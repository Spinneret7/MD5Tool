# MD5Tool
Simple tool for Generating md5 hashes and verifying them.
I made it a few months ago for using for my personal usage but decided to open source it.
### Compile:
```csc.exe /target:winexe /optimize+ /out:Md5Tool.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Md5Tool.cs```
You must need csc.exe even though it is present on most of the windows machines by default
### Usage:
It's not a Command Line Tool, just choose your folder and it will make the hash for you with a standalone exe containing a verifier and the md5 file itself. The tool is multi thread for both generating and verifying(Not much usable on HDD, It will use it's full powers on a SSD) 
### License
I Don't care for what purpose you use it, fork it, steal it, re encode everything you have freedom to do anything with this tool without my permissions!
