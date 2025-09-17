# Direct cladding operation for the ENCY CAM
This repository presents an example implementation of an operation for ENCY that calculates a toolpath for additive machining by directly transforming input geometric curves into tool movements at given speeds and with specified linking strategy parameters.

Here are presented both the source codes for studying the methods of programmatically generating a toolpath in the CAM system, and the final binary files, packed into a dext package ready for installation in a CAM system and use.

<img width="1918" height="1038" alt="ency_Ov5l1jIsUg" src="https://github.com/user-attachments/assets/e19861a7-4199-4253-a864-410de37f492d" />

## Installation instructions
1. Download and install the latest version of ENCY (https://encycam.com/download).

2. Select the latest version of the extension from the "Releases" section of this repository, download the **"DirectCladdingOperationExtension.dext"** file from the `Packages` section.

3. Start ENCY and open the Settings window on the "Extensions" tab.

4. Click to Install button and select the downloaded file "DirectCladdingOperationExtension.dext" to perform automatic installation.

5. Close the window and restart ENCY.

6. After that you should see the new "Direct Cladding" operation in the "Additive" section of the New operation window.

7. To use the operation, you need to create a new project, select the "Direct Cladding" operation and define the source set of geometric curves sliced into layers inside any CAD system and specify required parameters like tool diameter, cutting speed, printing strategy, sorting, etc.

## Build from source instructions

### Prerequisites
1. Install dotnet 8.0 SDK (https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

### Build from command line
1. Start commands\build.cmd.

### Build from Visual Studio Code
1. Open the root folder of the repository in Visual Studio Code. 

2. Open the command palette (Ctrl+Shift+P) and type "Tasks: Run Task" and select "build". Another way is to click on the "Terminal" menu item and select "Run task..." from the drop-down menu, then "build".

## How to debug
1. Download and install the latest version of ENCY (https://encycam.com/download).

2. Open the repo folder in Visual Studio Code.

3. Start debug session using "F5".

4. After the ENCY starts, open the Setup window on the "Extensions" tab and click on the "Install" button. 

5. Select the file "project\main\bin\Debug\DirectCladdingOperationExtension.settings.json"

6. Closet the window and ENCY.

7. Start debug session again using "F5".

8. Create and use "Direct cladding" operation.

9. When you click on the "Run" button in ENCY you should get into the "MakeWorkPath" method inside the "project\main\ExtensionToolPathCalculation.cs" file where you can set a breakpoint. 

