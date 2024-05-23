# Game Overview

This game is an automated simulation of several teams of spaceships trying to capture planets and eliminate all other teams.


## Planets

The game world consists of planets spawned at random in a sphere area. Planets generate resources, and have a random amount of moons around them.

A planet's resources are used by buildings to perform their function. Planets can generate up to 3 different types of resources.

A planet's moons are used as slots where buildings can be built. Buildings use the resources of the planet that the moon belongs to.


## Teams

Each team in the game starts off with a planet that belongs to them, as well as a Factory building that can produce ships.

Teams will handle building various different ship types in order to capture nearby planets, defend captured planets, and ensure a good distribution of resources among the planets they own.


## Ships

### Fighters

<img src="./Images/fighter-a.png" alt="fighter-a" height="200"/> <img src="./Images/fighter-b.png" alt="fighter-b" height="200"/> <img src="./Images/fighter-c.png" alt="fighter-c" height="200"/>

Fighter ships try to defend captured planets from enemies, and attack enemy planets to clear the way for workers to capture them.


### Workers

<img src="./Images/worker-a.png" alt="worker-a" height="200"/> <img src="./Images/worker-b.png" alt="worker-b" height="200"/>

Worker ships can capture planets, as well as build buildings on a planet's moons. Multiple workers can capture the same planet or build the same building at the same time, making the process faster.


### Traders

<img src="./Images/trader-a.png" alt="trader-a" height="200"/> <img src="./Images/trader-b.png" alt="trader-b" height="200"/>

Traders try to uniformly distribute resources across all the planets captured by a team. They find a planet in need of a certain resource, then find another planet that can give that resource, and then they will go take that resource from the giving planet to the receiving planet. 

Since not all planets generate all 3 types of resources, and some buildings mights need all 3 types of resources to perform certain actions, traders are key to ensuring that all planets can have all of their buildings function at full capacity.



## Buildings

### Factories

<img src="./Images/factory-a.png" alt="factory-a" height="200"/>

Factories produce ships based on what ship types the team's AI thinks are most important to produce at any moment. Each ship requires a certain amount of resources to produce.


### Turrets

<img src="./Images/turret-a.png" alt="turret-a" height="200"/> <img src="./Images/turret-b.png" alt="turret-b" height="200"/>

Turrets defent a planet and its buildings by shooting at enemies in range. Turret shots can consume planet resources.


### Research

<img src="./Images/research-a.png" alt="research-a" height="200"/> <img src="./Images/research-b.png" alt="research-b" height="200"/> <img src="./Images/research-c.png" alt="research-c" height="200"/>

Research buildings provide bonuses that can affect factory build speeds, planet resource generation rates, and ship attributes.
