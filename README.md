# Custom-Cursor-Switcher
This is a custom cursor switcher application for Windows 10, featuring a clean GUI with a bunch of custom cursors.

The Custom Cursor Switcher for Windows 10/11 is built to provide a seamless cursor switching experience to users. You will find the following features including:

1. A fresh, animated GUI with an in-built dark theme.
2. Cursor descriptions along with "Normal Cursor" previews designed to get an initial experience of a cursor pack before fully switching to it.
3. A revert button to instantly go back to your original cursor after reviewing.
4. An apply button to proceed with applying the cursor if you like the preview.

This app also shows your current cursor pack at the title bar of the window, thus saving your effort to find your currently applied cursor.

Currently, the app contains only 12 custom cursor packs along with the default "Modern Windows 10 Aero" pack. Future updates will guarantee a wider range of cursor packs and a custom integration which will allow you to build a custom pack of your own with cur/ani files.


## Installation Methods:

Method 1: (No install requireq)

Download the CursorSwitcher.exe and run it directly. No need to provide administrator privileges as the exe will automatically uplift its permissions to the required level.

Method 2: (Manual installation)

If the exe file does not work (may be due to differences in Operating System), download the CursorSwitcher_Build.zip file and extract it all into **one single folder**. Make sure that the CursorSwitcher.cs, app.manifest and the Build.exe batch file are in the same folder, otherwise the installation will be bugged. Next, run the Build.exe batch file by right clicking and selecting "Run As Administrator". 

This will compile the .cs file in accordance to the .manifest file using *your operating* system's C# Framework and create the finalized CursorSwitcher.exe file. Double click the exe file. If it asks for a storage location for the cursor files, select any location of your choice or use the default windows directory: *C:\Windows\Cursors*. This will store the custom cursor files in the designated folder. Remember, if you remove the cursor zip or delete the designated folder, you will be asked again to select a location for the files. After selecting the location, you will be able to access the app fully and switch between the available cursors.

*Note: If both methods fail, try running the Build.exe file using administrator permissions AND using the "Run in Windows 7" option available in the properties section. This will allow the build file to configure itself to an Older C# Framework. If it still fails, please report the issue back to me.*


If you encounter any bugs, please report it back to me. I will keep on actively updating the app with newer updates and possible patches in the near future.

Hope you enjoyed using my custom cursor app. Stay tuned for future updates.
