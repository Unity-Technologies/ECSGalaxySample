# ECS Galaxy Sample

<img src="./_Documentation/Images/GalaxySample.gif" alt="game" height="350"/>

> See [ProjectVersion](./ProjectSettings/ProjectVersion.txt) for minimum supported Unity version.

This sample demonstrates a large-scale simulation of teams of spaceships fighting for the control of planets. 

Fighter ships defend and attack planets, worker ships capture planets and build buildings on planet moons, and trader ships distribute resources across planets. Buildings use their planet's resources to build ships, provide defensive capabilities, or upgrade produced ships.

Several simulation parameters such as teams count, planet count, game area size, simulation speed, ship properties, etc... can be configured in the in-game 'Settings' menu. Some of these parameters can be tweaked in realtime as the simulation runs.


## Quick Start

To run the simulation, open the `Main` scene and press Play. A UI menu will appear and allow you to configure simulation settings. Once you're ready, press the "Simulate" button and the simulation will start. During play, the menu can be brought up again to tweak some settings in real time.

Controls:
* 'WASD' keys + 'Mouse' - control camera (only when menu is hidden).
* 'Escape' key - toggle in-game menu.
* 'Z' key - alternate between 3 camera modes (free camera, orbit planet, orbit ship).
* 'X' key - switch camera target in the orbit camera modes.
* 'Mouse Wheel' - zoom in/out for orbit cameras.
* 'Left Shift' - boost free camera speed.


## Documentation
* [Game Overview](./_Documentation/game-overview.md)
* [Code Overview](./_Documentation/code-overview.md)
* [Debug Views](./_Documentation/debug-views.md)
