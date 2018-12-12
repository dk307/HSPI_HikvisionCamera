Homeseer Video Stream Save PlugIn
=====================================
Overview
--------
This plugin captures ffmpeg supported streams (like rtsp) into a temp directory. If device is asked to record, it moves the recording to a specified permanent directory while discarding old recording.


This plugin uses ffmpeg for recording and conversion.

Compatibility
------------
Tested on the following platforms:
* Windows 10

 
Installation
-----------
Make sure that dotNet 4.7.2 is installed on machine. [Link](https://support.microsoft.com/en-us/help/4054531/microsoft-net-framework-4-7-2-web-installer-for-windows)

Place the compiled [executable](https://ci.appveyor.com/project/dk307/hspi-videostreamsave/build/artifacts?branch=master) and [config file](https://ci.appveyor.com/project/dk307/hspi-videostreamsave/build/artifacts?branch=master) in the HomeSeer installation directory. Restart HomeSeer. HomeSeer will recognize the plugin and will add plugin in disable state to its Plugins. Go to HomeSeer -> PlugIns -> Manage and enable this plugin. 
 
Build State
-----------
[![Build State](https://ci.appveyor.com/api/projects/status/github/dk307/HSPI_videostreamsave?branch=master&svg=true)](https://ci.appveyor.com/project/dk307/HSPI-videostreamsave/build/artifacts?branch=master)

  
