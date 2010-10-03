****************
 How to install
****************

Copy the following two files to
C:\Users\%UserName%\AppData\Roaming\Microsoft\VisualStudio\9.0\Addins
Please replace %UserName% with the actual user name. If you use other
version of Visual Studio than 9.0, replace it with the one you use.

File list:
	MKAddin\MKAddin.AddIn
	MKAddin\bin\MKAddin.dll

Do not forget to enable MKAddin plug-in at "Tools"->"Add-in Manager."
You can make it loaded on start-up there.


****************
   How to use
****************

Not all of the commands added by MKAddin are accessible from the toolbar.
We recommend that keyboard shotcut keys be registered. For you information,
my shortcut settings are:

	CTRL+O      ReTab
	CTRL+J      CancelRet
	CTRL+;      OperatorCompletion
	CTRL+K, M   AddCppUnitTestMethod
	ALT+;       SmartSemicolon
	CTRL+,      SwitchToPreviousDocument
	            AddCppAndH
	CTRK+K, C   ExtractConstant
	CTRK+K, T   MakeTupleConstructor

This add-in was tested with Visual Studio 2008 (English ver.) on Windows 7.
It may not work well with localized version of the Visual Studio.


****************
     License
****************

This work is distributed under the MIT License.


Copyright (c) 2010 Masahiro Kasahara

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.


