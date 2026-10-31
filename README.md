# Bomber Buddies — Unity edition

The realistic rebuild of Bomber Buddies: a procedurally generated island, first- or third-person
soldier, rifle, enemy infantry with pathfinding, waves, touch / gamepad / keyboard controls.
Everything in the world is built by code when the game starts, so there are no hand-made scenes to
keep in sync — the whole game is the C# in `Assets/BomberBuddies/Scripts`.

## What you need on the PC (one time)

1. **Unity Hub** → install **Unity 6.3 LTS** (the version in `ProjectSettings/ProjectVersion.txt`)
   with **Android Build Support** (including OpenJDK and Android SDK & NDK Tools) ticked.
2. **GitHub Desktop**, signed in to your GitHub account, with this repository cloned.
3. In Unity Hub → **Preferences → Licenses → Add → Get a free personal license** (this also creates
   the licence file the build server uses, see below).

Open the project from Unity Hub (Add → pick the cloned folder). The first open takes a few minutes:
the project sets itself up (render pipeline, materials, the island scene) and may ask to restart once
for the new input system. Then **Bomber Buddies → Open the island scene** and press **Play**.

Controls on PC: WASD move, mouse look, left button fire, right button aim, Shift sprint, Space jump,
C crouch, R reload, V first/third person, Esc frees the mouse.

## Builds without a PC (GitHub Actions)

Every push compiles the project on GitHub's servers; pushes to `main` also produce a Windows build and
an Android APK you can download from the run's **Artifacts**. For that the repository needs three
secrets (**Settings → Secrets and variables → Actions → New repository secret**):

| Secret | Value |
| --- | --- |
| `UNITY_LICENSE` | the whole text of `C:\ProgramData\Unity\Unity_lic.ulf` (created by the licence step above) |
| `UNITY_EMAIL` | the e-mail of the Unity account |
| `UNITY_PASSWORD` | its password |

A public repository gets unlimited free build minutes; a private one gets 2,000 minutes a month
(an Android build is roughly 30-40 minutes).

## Phones

Android: install the APK from the build artifacts (allow "unknown sources" once). iPhone needs a Mac
with Xcode or an Apple developer account for TestFlight — later.

## Layout

```
Assets/BomberBuddies/
  Scripts/Runtime/   the game (BomberBuddies assembly)
  Scripts/Editor/    project setup + command-line build (BomberBuddies.Editor assembly)
  Tests/Editor/      edit-mode tests run on every push
  Settings/          generated: render pipeline, material library  (not committed)
  Materials/         generated: materials and procedural textures   (not committed)
  Scenes/Island.unity generated: the one scene                      (not committed)
```
