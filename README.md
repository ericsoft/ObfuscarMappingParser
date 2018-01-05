[![GitHub License](https://img.shields.io/badge/license-MIT-brightgreen.svg?style=flat-square)](https://raw.githubusercontent.com/BrokenEvent/ObfuscarMappingParser/master/LICENSE)
[![Github Releases](https://img.shields.io/github/downloads/BrokenEvent/ObfuscarMappingParser/total.svg?style=flat-square)](https://github.com/BrokenEvent/ObfuscarMappingParser/releases)
======

# Introduction
*Obfuscar Mapping Parser* is a tool to read *Obfuscar's* Mapping.xml files and deobfuscate stacktraces created from obfuscated applications.

Just like this:

![Main purpose of the application](http://brokenevent.com/uploads/obfuscarparser/logo.png)
## Usecase
The main usecase is simple:

If you have an .NET application and used *Obfuscar* for it...

If the release obfuscated version of your application has crashed and you have a stacktrace...

If you don't afraid to use it to fix your application...

So just open Mapping.xml (generated by the *Obfuscar*) in *Obfuscar Mapping Parser* and deobfuscate your stacktrace.

## Features
* Parsing *Obfuscar* mapping files.
* Exploring and search in classes tree.
* Deobfuscation of the stacktraces.
* Analysis of items were missing in mapping and failed to deobfuscate.

# Some more to say...

## Other features

If your stacktrace is somehow broken (no parameters or results and so on), the *Obfuscar Mapping Parser* will try to substitute as much as it could. Even if obfuscated value was not added to mapping, the application will decode classname.

Another useful feature is PDB files. Microsoft Visual Studio stores there method-file mapping, so it is possible to get filename and line number for any method by its name. If you attach PDB file to *Obfuscar Mapping Parser*, it could open methods from items tree and Stacktrace analyzer in VS.

## Known problems

* If your stacktrace contains method from derived class which overrides a method of the base class, it would not be deobfuscated correctly. Just because this method of this class doesn't appear in the mapping file. It appears for the base class, but not for the derived. And the *Obfuscar Mapping Parser* is no way to know that current class inherits the base.

## Future plans

* Assembly and reflection support. This will allow to solve issues with inheritance and also may improve the quality of stacktrace deobfuscation.
* To made something like Visual Studio plugin for *Obfuscar Mapping Parser*. This will allow to process stacktraces just from IDE.

## Notification

This project is not connected with the original *Obfuscar*.

To get the original .NET obfuscation tool please follow here:

[http://www.obfuscar.com/](http://www.obfuscar.com/)

All copyrights to the *"Obfuscar"* name are reserved to its author.

## P.S.

Official web-page: [brokenevent.com/projects/obfuscarparser](http://brokenevent.com/projects/obfuscarparser)
