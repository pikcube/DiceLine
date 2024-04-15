# Dice Line

A utility for rolling dice using the command line.

The syntax is just DiceLine d20 to roll a d20.

If you want to roll multiple, you can do that with 2d6.

Want to add static mods. Go for it 3d10+5.

Roll multiple types of dice at once? Sure! 2d10+3d6+5

Need to subtract a modifier or a die. That works too! d20-d6-2+3

## Installation

To install, download the release in the sidebar. Running the exe requires the .dll to be present. Otherwise, the .dll can be passed to the .net runtime directly.

.net8.0 is not bundled and must be intalled manually.

## Public API

If you want to reference DiceLine.dll in your app, the classes are public with documentaiton. I plugged this straight into my discord bot that way.

## Install API

The package is available through nuget. https://www.nuget.org/packages/DiceLine/1.0.1