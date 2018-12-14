Homeseer Hikvision Camera PlugIn
=====================================
Overview
--------
This plugin integrates with Hikvision cameras using ISAPI

It can:
* Poll and set properties of camera
* Listens to alarm stream
* Take snapshots
* Download videos from camera 

Compatibility
------------
Tested on the following platforms:
* Windows 10

 
Installation
-----------
Make sure that dotNet 4.7.2 is installed on machine. [Link](https://support.microsoft.com/en-us/help/4054531/microsoft-net-framework-4-7-2-web-installer-for-windows)

Place the compiled [executable](https://ci.appveyor.com/project/dk307/hspi-hikvisioncamera/build/artifacts?branch=master) and [config file](https://ci.appveyor.com/project/dk307/hspi-hikvisioncamera/build/artifacts?branch=master) in the HomeSeer installation directory. Restart HomeSeer. HomeSeer will recognize the plugin and will add plugin in disable state to its Plugins. Go to HomeSeer -> PlugIns -> Manage and enable this plugin. 
 
Build State
-----------
[![Build State](https://ci.appveyor.com/api/projects/status/github/dk307/HSPI_hikvisioncamera?branch=master&svg=true)](https://ci.appveyor.com/project/dk307/HSPI-hikvisioncamera/build/artifacts?branch=master)

  
