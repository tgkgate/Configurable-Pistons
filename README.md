
    Configurable Pistons

    Author:    Gate
    Date:      2021/08/14

    Version:   1.2

    Change-Log:
                v1.2:		Added Auto Rectract and Extend support
                v1.1:		Bug-fix INI related
                v1.0:		Initial Release

    Arguments:
        This script understands the following arguments
            reset   - This resets all internal caches.
                      Run this if you add/remove or change piston settings
            clear   - This clears the programmable block message panel
                      for development use

    Usage:
        Add (and edit) the following to the custom data of any piston you wish to add script functionality

        [Piston Settings]
        RetractSpeed=1
        ExtendSpeed=1
        AutoRectract=False
        AutoExtend=False

    Note:
        RetractSpeed - The speed (in m/s) the piston should move during Retraction
        ExtendSpeed  - The speed (in m/s) the piston should move during Extension
        AutoRetract  - Should the piston automatically retract once at full extension
        AutoExtend   - Should the piston automatically extend once at full retraction

    
        You MUST either recompile the script,
        or run with argument "reset"
        to populate piston cache


    Script (C)opyright 2021 Gate
    Some Rights Reserved.

    Space Engineers is (C)opyright Keen Software House
    https://www.spaceengineersgame.com/
    

    Created using the MDK-SE SDK
    Malware's Development Kit for Space Engineers
    https://github.com/malware-dev/MDK-SE


This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.
To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/4.0/
or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
