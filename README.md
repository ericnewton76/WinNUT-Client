# WinNUT-Client

## Installation
To use it, please follow the following steps:
1. Get the [last available Releases](https://github.com/gawindx/WinNUT-Client/releases)
2. Install WinNUT-Client using the "WinNUT-Setup.msi" file obtained previously
3. If you were using an older version of WinNUT (v1.x), copy your "ups.ini" configuration file to the WinNUT-Client installation directory (by default "C:\Program Files(x86)\WinNUT-Client ") for an automatic import of your parameters during the first launch
4. Start WinNUT V2 and modify the parameters according to your needs

## Specific Configuration

### For Synology NAS 
If your NUT server is hosted on a Synology NAS, be sure to provide the following connection information (default):
Login: upsmon
Password: secret

It will probably be necessary to allow the WinNUT-Client IP to communicate with the NUT server.
*See issue 47 for more information, specifically [this commentary](https://github.com/gawindx/WinNUT-Client/issues/47#issuecomment-759180793).*

## Update WinNUT-Client

Since version 1.8.0.0, WinNUT-Client includes a process for checking for updates.
This process can be started automatically on startup or manually on demand (and you can choose whether you want to update from the stable or development version)

During this update, the new installation file will be automatically downloaded and you can easily update your version of WinNUT-Client.

This process is fully integrated and no longer requires a second executable.

## Third Party Components / Acknowledgments

WinNUT-Client uses:
- a modified version of AGauge initially developed by [Code-Artist](https://github.com/Code-Artist/AGauge) and under [MIT license](https://opensource.org/licenses/MIT)
- Class IniReader developed by [Ludvik Jerabek](https://www.codeproject.com/Articles/21896/INI-Reader-Writer-Class-for-C-VB-NET-and-VBScript) and under [The Code Project Open License](http://www.codeproject.com/info/cpol10.aspx)
- Newtonsoft.Json Library is used in this Project [Newtonsoft.json Website](https://www.newtonsoft.com/json) and under [MIT license](https://opensource.org/licenses/MIT)

## License

WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)

This program is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License as published by the Free Software Foundation, either version 3 of the
License, or any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

## Donation
If you want to support this project or reward the work done, you can do so here:

[![paypal](https://www.paypalobjects.com/en_US/FR/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate?hosted_button_id=FAFJ3ZKMENGCU)
