using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The link between phones: the original's Net / SB layer rebuilt on Unity Netcode.
    /// Two ways in, like the original: "Online, anywhere" through Unity Relay with a room code the host reads out, and
    /// "Nearby, same room" over the same Wi-Fi with no internet (the host shouts a beacon, the others list it).
    /// Everything the game sends goes through one named message as compact binary; the host is the authority for
    /// the enemies, every phone is the authority for its own pilot.</summary>
    public class NetSession : MonoBehaviour
    {
        public static NetSession Instance { get; private set; }
        public enum Role { None, Host, Join }
        public Role role = Role.None;
        public bool Active { get { return role != Role.None && nm != null && (nm.IsHost || nm.IsConnectedClient); } }
        public bool IsHost { get { return role == Role.Host; } }
        public bool online;                 // Relay (true) or Nearby (false)
        public string joinCode = "";        // the room code (online) or our address (nearby)
        public string status = "";
        public string callSign = "";
        public string lobbyMode = "battle"; // the host's choice of game
        public bool started;                // the host has started (or is in) a battle
        public bool nearbyUnsupported;
        public float rtt = -1f;

        public class Peer { public ulong id; public string name; public string craft = "plane"; public int kills, deaths; public bool ready; }
        public readonly Dictionary<ulong, Peer> peers = new Dictionary<ulong, Peer>();
        public ulong MyId { get { return nm != null ? nm.LocalClientId : 0; } }
        public string NameOf(ulong id) { Peer p; return peers.TryGetValue(id, out p) ? p.name : "Buddy"; }

        /// <summary>Raised when the lobby changes (peers, mode, status), so screens can redraw.</summary>
        public event Action Changed;
        /// <summary>Raised when the host starts the battle: everyone loads it.</summary>
        public event Action<string> Started;
        /// <summary>In-game messages (opcode >= 32) are handed to NetSync.</summary>
        public Action<ulong, byte, FastBufferReader> OnGameMessage;

        NetworkManager nm; UnityTransport transport;
        const string Msg = "bb";
        const int LanPort = 7777, BeaconPort = 47777;
        UdpClient beacon; float beaconT; bool listening;
        public readonly List<KeyValuePair<string, string>> nearby = new List<KeyValuePair<string, string>>();   // address → host name
        readonly Dictionary<string, float> nearbySeen = new Dictionary<string, float>();

        public static NetSession Get()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("NetSession"); DontDestroyOnLoad(go);
            Instance = go.AddComponent<NetSession>();
            Instance.callSign = PlayerPrefs.GetString("bb_name", "Pilot " + UnityEngine.Random.Range(1000, 9999));
            return Instance;
        }

        void EnsureManager()
        {
            if (nm != null) return;
            nm = gameObject.AddComponent<NetworkManager>();
            transport = gameObject.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = true, TickRate = 30 };
            nm.ConnectionApprovalCallback = (req, res) =>
            {
                // everyone must be on the same game version, like the original
                string v = req.Payload != null && req.Payload.Length > 0 ? Encoding.UTF8.GetString(req.Payload) : "";
                bool ok = v == Application.version;
                res.Approved = ok; res.CreatePlayerObject = false; res.Reason = ok ? "" : "Different game version (" + v + " vs " + Application.version + ")";
            };
            nm.OnClientConnectedCallback += OnConnected;
            nm.OnClientDisconnectCallback += OnDisconnected;
            nm.OnTransportFailure += () => { status = "The link dropped."; Leave(); };
        }

        // ------------------------------------------------------------------ hosting and joining
        public async void HostOnline() { await HostOnlineAsync(); }
        public async Task HostOnlineAsync()
        {
            EnsureManager(); online = true; status = "Getting a room code…"; Fire();
            try
            {
                await SignIn();
                var alloc = await RelayService.Instance.CreateAllocationAsync(7);
                string code = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                transport.SetRelayServerData(new RelayServerData(alloc, "udp"));
                joinCode = code.ToUpperInvariant();
                StartHostCommon();
                status = "Room " + joinCode + " · read it out. Waiting for buddies.";
            }
            catch (Exception e) { status = OnlineError(e); role = Role.None; }
            Fire();
        }

        public async void JoinOnline(string code) { await JoinOnlineAsync(code); }
        public async Task JoinOnlineAsync(string code)
        {
            EnsureManager(); online = true; code = (code ?? "").Trim().ToUpperInvariant(); joinCode = code; status = "Joining " + code + "…"; Fire();
            try
            {
                await SignIn();
                var ja = await RelayService.Instance.JoinAllocationAsync(code);
                transport.SetRelayServerData(new RelayServerData(ja, "udp"));
                StartClientCommon();
            }
            catch (Exception e) { status = OnlineError(e); role = Role.None; }
            Fire();
        }

        static string OnlineError(Exception e)
        {
            string m = e.Message ?? "";
            if (m.Contains("project") || m.Contains("Project") || m.Contains("not linked") || m.Contains("No project ID")) return "Online needs this build linked to a Unity project (cloud project id). Nearby still works.";
            if (m.Contains("join code") || m.Contains("JoinCode") || m.Contains("not found") || m.Contains("Not Found")) return "No room with that code. Check it with the host.";
            return "Could not connect: " + m;
        }

        async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public void HostNearby()
        {
            EnsureManager(); online = false;
            try
            {
                transport.SetConnectionData("0.0.0.0", (ushort)LanPort, "0.0.0.0");
                StartHostCommon();
                joinCode = LocalAddress();
                StartBeacon();
                status = "Hosting on this Wi-Fi as " + callSign + ". Waiting for buddies.";
            }
            catch (Exception e) { status = "Could not host: " + e.Message; role = Role.None; }
            Fire();
        }

        public void JoinNearby(string address)
        {
            EnsureManager(); online = false; joinCode = address; status = "Joining " + address + "…"; Fire();
            try { transport.SetConnectionData(address, (ushort)LanPort); StartClientCommon(); }
            catch (Exception e) { status = "Could not join: " + e.Message; role = Role.None; }
            Fire();
        }

        void StartHostCommon()
        {
            nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Application.version);
            role = Role.Host; peers.Clear();
            if (!nm.StartHost()) throw new Exception("the host could not start");
            Register();
            peers[nm.LocalClientId] = new Peer { id = nm.LocalClientId, name = callSign, craft = Progress.Craft };
        }
        void StartClientCommon()
        {
            nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Application.version);
            role = Role.Join; started = false; peers.Clear();
            if (!nm.StartClient()) throw new Exception("the client could not start");
            Register();
        }
        void Register() { nm.CustomMessagingManager.RegisterNamedMessageHandler(Msg, OnMessage); }

        public void Leave()
        {
            StopBeacon(); StopListening();
            if (nm != null && (nm.IsHost || nm.IsClient)) nm.Shutdown();
            role = Role.None; started = false; peers.Clear(); joinCode = ""; rtt = -1f;
            Fire();
        }

        void OnConnected(ulong id)
        {
            if (role == Role.Host)
            {
                if (id != nm.LocalClientId) { peers[id] = new Peer { id = id, name = "Buddy" }; status = NameOf(id) + " joined."; }
                SendLobby();
                if (started) { var w = new FastBufferWriter(64, Unity.Collections.Allocator.Temp); w.WriteValueSafe((byte)3); w.WriteValueSafe(lobbyMode); nm.CustomMessagingManager.SendNamedMessage(Msg, id, w, NetworkDelivery.ReliableSequenced); }
            }
            else if (id == nm.LocalClientId)
            {
                status = "Linked. Waiting for the host to start.";
                var w = new FastBufferWriter(128, Unity.Collections.Allocator.Temp); w.WriteValueSafe((byte)1); w.WriteValueSafe(callSign); w.WriteValueSafe(Progress.Craft);
                nm.CustomMessagingManager.SendNamedMessage(Msg, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
            }
            Fire();
        }
        void OnDisconnected(ulong id)
        {
            if (role == Role.Host) { if (peers.ContainsKey(id)) { status = NameOf(id) + " left."; peers.Remove(id); } SendLobby(); if (NetSync.Instance != null) NetSync.Instance.PeerLeft(id); }
            else if (id == nm.LocalClientId || id == NetworkManager.ServerClientId)
            {
                string why = nm.DisconnectReason; status = string.IsNullOrEmpty(why) ? "The host closed the game." : why;
                role = Role.None; started = false; peers.Clear();
                if (NetSync.Instance != null) NetSync.Instance.HostGone();
            }
            Fire();
        }

        // ------------------------------------------------------------------ lobby messages (opcodes < 32)
        // 1 hello(name, craft) client→host · 2 lobby(mode, started, n, [id,name,craft,kills,deaths]) host→all · 3 start(mode) host→all
        // 4 setmode(mode) host→all · 5 ping(t) · 6 pong(t)
        void SendLobby()
        {
            var w = new FastBufferWriter(1024, Unity.Collections.Allocator.Temp);
            w.WriteValueSafe((byte)2); w.WriteValueSafe(lobbyMode); w.WriteValueSafe(started); w.WriteValueSafe(peers.Count);
            foreach (var p in peers.Values) { w.WriteValueSafe(p.id); w.WriteValueSafe(p.name ?? "Buddy"); w.WriteValueSafe(p.craft ?? "plane"); w.WriteValueSafe(p.kills); w.WriteValueSafe(p.deaths); }
            nm.CustomMessagingManager.SendNamedMessageToAll(Msg, w, NetworkDelivery.ReliableSequenced);
        }
        public void SetMode(string mode) { if (role != Role.Host) return; lobbyMode = mode; SendLobby(); Fire(); }
        public void SetCraft(string craft) { Peer p; if (nm != null && peers.TryGetValue(MyId, out p)) { p.craft = craft; if (role == Role.Host) SendLobby(); else SendHello(); } }
        void SendHello() { var w = new FastBufferWriter(128, Unity.Collections.Allocator.Temp); w.WriteValueSafe((byte)1); w.WriteValueSafe(callSign); w.WriteValueSafe(Progress.Craft); nm.CustomMessagingManager.SendNamedMessage(Msg, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced); }

        /// <summary>Everyone loads the host's battle (the scene reloads; this object survives it).</summary>
        void LoadBattle(string mode)
        {
            Debug.Log("LoadBattle " + mode);
            Time.timeScale = 1f;
            GameBootstrap.ChosenMode = mode;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Host: start the battle for everyone.</summary>
        public void StartBattle(string mode)
        {
            if (role != Role.Host) return;
            lobbyMode = mode; started = true;
            var w = new FastBufferWriter(64, Unity.Collections.Allocator.Temp); w.WriteValueSafe((byte)3); w.WriteValueSafe(mode);
            nm.CustomMessagingManager.SendNamedMessageToAll(Msg, w, NetworkDelivery.ReliableSequenced);
            if (Started != null) Started(mode);
            LoadBattle(mode);
        }

        void OnMessage(ulong from, FastBufferReader r)
        {
            byte op; r.ReadValueSafe(out op);
            if (op >= 32) { if (OnGameMessage != null) OnGameMessage(from, op, r); return; }
            switch (op)
            {
                case 1: { string n, c; r.ReadValueSafe(out n); r.ReadValueSafe(out c); Peer p; if (!peers.TryGetValue(from, out p)) peers[from] = p = new Peer { id = from }; p.name = string.IsNullOrEmpty(n) ? "Buddy" : n; p.craft = c; status = p.name + " is in."; if (role == Role.Host) SendLobby(); Fire(); break; }
                case 2:
                    {
                        string m; bool s; int n; r.ReadValueSafe(out m); r.ReadValueSafe(out s); r.ReadValueSafe(out n);
                        lobbyMode = m; if (role == Role.Join) started = s; peers.Clear();
                        for (int i = 0; i < n; i++) { ulong id; string nm2, c; int k, d; r.ReadValueSafe(out id); r.ReadValueSafe(out nm2); r.ReadValueSafe(out c); r.ReadValueSafe(out k); r.ReadValueSafe(out d); peers[id] = new Peer { id = id, name = nm2, craft = c, kills = k, deaths = d }; }
                        Fire(); break;
                    }
                case 3: { string m; r.ReadValueSafe(out m); lobbyMode = m; started = true; Fire(); if (Started != null) Started(m); if (GameBootstrap.LastMode != m || NetSync.Instance == null) LoadBattle(m); break; }
                case 5: { float t; r.ReadValueSafe(out t); var w = new FastBufferWriter(16, Unity.Collections.Allocator.Temp); w.WriteValueSafe((byte)6); w.WriteValueSafe(t); nm.CustomMessagingManager.SendNamedMessage(Msg, from, w, NetworkDelivery.Unreliable); break; }
                case 6: { float t; r.ReadValueSafe(out t); rtt = (Time.realtimeSinceStartup - t) * 1000f; break; }
            }
        }

        /// <summary>In-game: send a message to the host (or, as the host, to everyone / one client).</summary>
        public void SendToHost(FastBufferWriter w, bool reliable) { if (nm == null) return; nm.CustomMessagingManager.SendNamedMessage(Msg, NetworkManager.ServerClientId, w, reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.Unreliable); }
        public void SendToAll(FastBufferWriter w, bool reliable) { if (nm == null) return; nm.CustomMessagingManager.SendNamedMessageToAll(Msg, w, reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.Unreliable); }
        public void SendTo(ulong id, FastBufferWriter w, bool reliable) { if (nm == null) return; nm.CustomMessagingManager.SendNamedMessage(Msg, id, w, reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.Unreliable); }
        public void SendToOthers(ulong except, FastBufferWriter w, bool reliable)
        {
            if (nm == null) return;
            foreach (var id in peers.Keys) if (id != except && id != nm.LocalClientId) nm.CustomMessagingManager.SendNamedMessage(Msg, id, w, reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.Unreliable);
        }
        public void Stat(int kills, int deaths) { Peer p; if (peers.TryGetValue(MyId, out p)) { p.kills = kills; p.deaths = deaths; } }

        float pingT;
        void Update()
        {
            if (role == Role.Join && Active) { pingT -= Time.unscaledDeltaTime; if (pingT <= 0f) { pingT = 2f; var w = new FastBufferWriter(16, Unity.Collections.Allocator.Temp); w.WriteValueSafe((byte)5); w.WriteValueSafe(Time.realtimeSinceStartup); SendToHost(w, false); } }
            if (beacon != null && role == Role.Host) { beaconT -= Time.unscaledDeltaTime; if (beaconT <= 0f) { beaconT = 1f; Shout(); } }
            if (listening) PollBeacons();
        }

        void Fire() { if (Changed != null) Changed(); }

        // ------------------------------------------------------------------ nearby: a UDP beacon on the Wi-Fi
        static string LocalAddress()
        {
            try { foreach (var a in Dns.GetHostEntry(Dns.GetHostName()).AddressList) if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) return a.ToString(); } catch { }
            try { using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) { s.Connect("8.8.8.8", 65530); return ((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch { }
            return "127.0.0.1";
        }
        void StartBeacon() { try { beacon = new UdpClient(); beacon.EnableBroadcast = true; beaconT = 0f; } catch (Exception e) { nearbyUnsupported = true; status = "Nearby is not available here: " + e.Message; } }
        void Shout()
        {
            try { var b = Encoding.UTF8.GetBytes("BB|" + Application.version + "|" + callSign + "|" + LocalAddress()); beacon.Send(b, b.Length, new IPEndPoint(IPAddress.Broadcast, BeaconPort)); } catch { }
        }
        void StopBeacon() { if (beacon != null) { try { beacon.Close(); } catch { } beacon = null; } }
        UdpClient ear;
        public void StartListening()
        {
            if (listening) return;
            try { ear = new UdpClient(); ear.ExclusiveAddressUse = false; ear.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); ear.Client.Bind(new IPEndPoint(IPAddress.Any, BeaconPort)); ear.Client.Blocking = false; listening = true; nearby.Clear(); nearbySeen.Clear(); }
            catch (Exception e) { nearbyUnsupported = true; status = "Nearby is not available here: " + e.Message; Fire(); }
        }
        public void StopListening() { listening = false; if (ear != null) { try { ear.Close(); } catch { } ear = null; } }
        void PollBeacons()
        {
            bool changed = false;
            try
            {
                while (ear != null && ear.Available > 0)
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0); var b = ear.Receive(ref ep); var s = Encoding.UTF8.GetString(b).Split('|');
                    if (s.Length < 4 || s[0] != "BB" || s[1] != Application.version) continue;
                    string addr = ep.Address.ToString(); if (!nearbySeen.ContainsKey(addr)) { nearby.Add(new KeyValuePair<string, string>(addr, s[2])); changed = true; }
                    nearbySeen[addr] = Time.unscaledTime;
                }
            }
            catch (SocketException) { }
            // forget hosts that stopped shouting
            for (int i = nearby.Count - 1; i >= 0; i--) { float t; if (nearbySeen.TryGetValue(nearby[i].Key, out t) && Time.unscaledTime - t > 4f) { nearbySeen.Remove(nearby[i].Key); nearby.RemoveAt(i); changed = true; } }
            if (changed) Fire();
        }

        void OnDestroy() { StopBeacon(); StopListening(); }
    }
}
