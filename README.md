# Meta XR Audio SDK Demo

A small VR tour of spatial audio on Meta Quest, built with Unity 6 and the Meta XR Audio SDK.
Four scenes, each about one concept, each with a visualization that shows what you are hearing.

![Ambisonics, point source, room acoustics and source directivity](docs/images/banner.jpg)

## The scenes

Every scene has a button (or a door) that takes you to the next one, and the last scene loops back to the first.

### 1. Ambisonics

A first-order ambisonic recording of a park plays all around you.
The globe on the microphone bulges and glows towards wherever the sound is coming from right now.

<p>
  <img src="docs/images/ambisonics-soundfield.jpg" width="49%" alt="Soundfield globe on an ambisonic microphone">
  <img src="docs/images/ambisonics-sign.jpg" width="49%" alt="Welcome sign in the park">
</p>

### 2. Point source

A whispering ember soul hovers above the altar and pulses with its own loudness.
Grab it, carry it around and hear the spatialization follow.

<p>
  <img src="docs/images/pointsource-ember.jpg" width="49%" alt="Ember soul point source with a ripple">
  <img src="docs/images/pointsource-altar.jpg" width="49%" alt="The altar under a blood-red sun">
</p>

### 3. Room acoustics

Step into a garage where a power drill runs in bursts.
Reflections and reverb come from the Room Acoustic Properties object, and the drill is still grabbable.

<p>
  <img src="docs/images/roomacoustics-garage.jpg" width="49%" alt="The garage">
  <img src="docs/images/roomacoustics-drill.jpg" width="49%" alt="The power drill on the workbench">
</p>

### 4. Source directivity

Three statues talk to each other in a ruined temple.
Glowing motes stream from their mouths in the pattern of a human voice, so walking behind a speaker both looks and sounds different.

<p>
  <img src="docs/images/directivity-ruins.jpg" width="49%" alt="Temple ruins from above">
  <img src="docs/images/directivity-statues.jpg" width="49%" alt="Speaking statues with voice motes">
</p>

## Getting started

1. Clone the repository.
2. Open the project with Unity **6000.3.5f2** (with Android build support).
3. Switch the platform to Android and build to a Quest.
   The tour starts in `Assets/Scenes/Ambisonics.unity`.

Packages (Meta XR Audio SDK 83, Meta XR Core and Interaction SDK, URP 17) are restored automatically on first open.

## Where things are

| Folder | Contents |
| --- | --- |
| `Assets/Scenes` | The four scenes, in build order |
| `Assets/Scripts` | Visualizers, drill, door and scene loading |
| `Assets/Editor` | Helpers under **Tools > MetaAudioSDK Demo** |
| `Assets/Tests` | Play mode test that walks the whole scene loop |
| `Assets/ThirdParty` | Asset packs used for the environments |
| `SourceArt` | Blender sources of the custom models |
