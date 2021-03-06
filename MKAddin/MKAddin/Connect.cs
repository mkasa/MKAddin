using System;
using System.Resources;
using System.Reflection;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using System.IO;

namespace MKAddin
{
	/// <summary>The object for implementing an Add-in.</summary>
	/// <seealso class='IDTExtensibility2' />
	public class Connect : IDTExtensibility2, IDTCommandTarget
	{
        // The function below is from http://www.csharp-examples.net/inputbox/
        public static DialogResult InputBox(string title, string promptText, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }

        private Boolean ContainsNonSpaceChar(String s)
        {
            for(int i = 0; i < s.Length; i++) {
                if (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n') continue;
                return true;
            }
            return false;
        }

        private String SPTrim(String s)
        {
            int st, ed;
            for(st = 0; st < s.Length; st++) {
                char c = s[st];
                if(c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    break;
            }
            for(ed = s.Length; 0 < ed; ed--) {
                char c = s[ed - 1];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    break;
            }
            return s.Substring(st, ed - st);
        }

        private String GetPreviousPhrase(ref TextSelection t)
        {
            t.WordLeft(false, 1);
            t.WordLeft(false, 1);
            t.WordRight(true, 1);
            String s = t.Text;
            t.Text = "";
            t.WordRight(true, 1);
            t.Text = "";
            return SPTrim(s);
        }

        private void GetTypeAndNameForOperatorCompletion(ref TextSelection ts, out String typeName, out String varName)
        {
            ts.StartOfLine(vsStartOfLineOptions.vsStartOfLineOptionsFirstColumn, false);
            ts.EndOfLine(true);
            String lineText = SPTrim(ts.Text);
            int lastGtPos = lineText.IndexOf('>');
            if(lastGtPos == -1) {
                // non-template
                int lastSpPos = lineText.IndexOf(' ');
                typeName = SPTrim(lineText.Substring(0, lastSpPos));
                lineText = SPTrim(lineText.Substring(lastSpPos + 1));
            } else {
                // template code
                int prevGtPos;
                do {
                    prevGtPos = lastGtPos;
                    lastGtPos = lineText.IndexOf('>', lastGtPos + 1);
                } while(lastGtPos >= 0);
                typeName = SPTrim(lineText.Substring(0, prevGtPos + 1));
                lineText = SPTrim(lineText.Substring(prevGtPos + 1));
            }
            int spPos = lineText.IndexOf(' ');
            if(spPos < 0) {
                varName = SPTrim(lineText);
            } else {
                varName = SPTrim(lineText.Substring(0, spPos));
            }
        }

        private String GetOperatorNameForOperatorCompletion(ref TextSelection ts)
        {
            ts.WordLeft(false, 1);
            ts.WordRight(true, 1);
            return ts.Text;
        }

        private String GetOneCPPToken(String str, int startPosition)
        {
            int strLen = str.Length;
            while (startPosition < strLen)
            {
                char c = str[startPosition];
                if(c != ' ' && c != '\t')
                    break;
                startPosition++;
            }
            int parenNestLevel = 0;
            int cursor = startPosition;
            while (cursor < strLen)
            {
                char c = str[cursor];
                if(c == '<' || c == '(') {
                    parenNestLevel++;
                } else if(c == '>' || c == ')') {
                    if(0 < parenNestLevel) {
                        parenNestLevel--;
                    } else {
                        return "Unbalanced parenthesis";
                    }
                } else if(c == ' ' || c == ',') {
                    if(parenNestLevel <= 0)
                        break;
                }
                cursor++;
            }
            return str.Substring(startPosition, cursor - startPosition);
        }

		/// <summary>Implements the constructor for the Add-in object. Place your initialization code within this method.</summary>
		public Connect()
		{
		}

		/// <summary>Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded.</summary>
		/// <param term='application'>Root object of the host application.</param>
		/// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
		/// <param term='addInInst'>Object representing this Add-in.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
		{
			_applicationObject = (DTE2)application;
			_addInInstance = (AddIn)addInInst;
			if(connectMode == ext_ConnectMode.ext_cm_UISetup)
			{
				object []contextGUIDS = new object[] { };
				Commands2 commands = (Commands2)_applicationObject.Commands;
				string toolsMenuName;

				try
				{
					//If you would like to move the command to a different menu, change the word "Tools" to the 
					//  English version of the menu. This code will take the culture, append on the name of the menu
					//  then add the command to that menu. You can find a list of all the top-level menus in the file
					//  CommandBar.resx.
					string resourceName;
					ResourceManager resourceManager = new ResourceManager("MKAddin.CommandBar", Assembly.GetExecutingAssembly());
					CultureInfo cultureInfo = new CultureInfo(_applicationObject.LocaleID);
					
					if(cultureInfo.TwoLetterISOLanguageName == "zh")
					{
						System.Globalization.CultureInfo parentCultureInfo = cultureInfo.Parent;
						resourceName = String.Concat(parentCultureInfo.Name, "Tools");
					}
					else
					{
                        resourceName = String.Concat(cultureInfo.TwoLetterISOLanguageName, "Tools");
					}
					toolsMenuName = resourceManager.GetString(resourceName);
				}
				catch
				{
					//We tried to find a localized version of the word Tools, but one was not found.
					//  Default to the en-US word, which may work for the current culture.
					toolsMenuName = "Tools";
				}

				//Place the command on the tools menu.
				//Find the MenuBar command bar, which is the top-level command bar holding all the main menu items:
				Microsoft.VisualStudio.CommandBars.CommandBar menuBarCommandBar = ((Microsoft.VisualStudio.CommandBars.CommandBars)_applicationObject.CommandBars)["MenuBar"];

				//Find the Tools command bar on the MenuBar command bar:
				CommandBarControl toolsControl = menuBarCommandBar.Controls[toolsMenuName];
				CommandBarPopup toolsPopup = (CommandBarPopup)toolsControl;

				//This try/catch block can be duplicated if you wish to add multiple commands to be handled by your Add-in,
				//  just make sure you also update the QueryStatus/Exec method to include the new command names.
				try {
                    Command command = commands.AddNamedCommand2(_addInInstance, "AlignOp", "Align Texts", "Align Texts", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    if ((command != null) && (toolsPopup != null)) {
                        // Add a control for the command to the tools menu:
						// command.AddControl(toolsPopup.CommandBar, 1);
					}
				} 
                catch(System.ArgumentException) { }
                try {
                    Command command = commands.AddNamedCommand2(_addInInstance, "ReTab", "Get Cursor Indented", "Get Cursor Indented", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    if ((command != null) && (toolsPopup != null))
                    { // Add a control for the command to the tools menu:
                        // command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException) { }
                try
                {
                    Command command = commands.AddNamedCommand2(_addInInstance, "CancelRet", "Cancel NewLine", "Cancel NewLine", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    if ((command != null) && (toolsPopup != null))
                    { // Add a control for the command to the tools menu:
                        // command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException) { }
                try
                {
                    Command command = commands.AddNamedCommand2(_addInInstance, "OperatorCompletion", "Operator Completion", "Operator Completion", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    if ((command != null) && (toolsPopup != null))
                    { // Add a control for the command to the tools menu:
                        // command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException) { }
                try
                {
                    Command command = commands.AddNamedCommand2(_addInInstance, "AddCppUnitTestMethod", "Add Test Method for CppUnit", "Add Test Method for CppUnit", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    if ((command != null) && (toolsPopup != null))
                    { // Add a control for the command to the tools menu:
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException) { }
                try
                {
                    Command command = commands.AddNamedCommand2(_addInInstance, "SmartSemicolon", "Smart Semicolon", "Smart Semicolon", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    if ((command != null) && (toolsPopup != null))
                    { // Add a control for the command to the tools menu:
                        // command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException) { }
                try
                {
                    Command command = commands.AddNamedCommand2(_addInInstance, "SwitchToPreviousDocument", "Switch to prev. doc.", "Switch to the previous document", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    if ((command != null) && (toolsPopup != null))
                    { // Add a control for the command to the tools menu:
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException) { }
                try
                {
                    Command command = commands.AddNamedCommand2(_addInInstance, "AddCppAndH", "Add Cpp/H", "Add a cpp file and a header file at once to the active project", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    if ((command != null) && (toolsPopup != null))
                    { // Add a control for the command to the tools menu:
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException) { }
                try
                {
                    Command command = commands.AddNamedCommand2(_addInInstance, "ExtractConstant", "Extract Constant", "Add a const variable just above the cursor", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    if ((command != null) && (toolsPopup != null))
                    { // Add a control for the command to the tools menu:
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException) { }
                try
                {
                    Command command = commands.AddNamedCommand2(_addInInstance, "MakeTupleConstructor", "Create Tuple Constructor", "Create a constructor that initializes the member variables by a tuple", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    if ((command != null) && (toolsPopup != null))
                    { // Add a control for the command to the tools menu:
                        command.AddControl(toolsPopup.CommandBar, 1);
                    }
                }
                catch (System.ArgumentException) { }
            }
		}

		/// <summary>Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being unloaded.</summary>
		/// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
		}

		/// <summary>Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection of Add-ins has changed.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />		
		public void OnAddInsUpdate(ref Array custom)
		{
		}

		/// <summary>Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host application has completed loading.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnStartupComplete(ref Array custom)
		{
		}

		/// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnBeginShutdown(ref Array custom)
		{
		}
		
		/// <summary>Implements the QueryStatus method of the IDTCommandTarget interface. This is called when the command's availability is updated</summary>
		/// <param term='commandName'>The name of the command to determine state for.</param>
		/// <param term='neededText'>Text that is needed for the command.</param>
		/// <param term='status'>The state of the command in the user interface.</param>
		/// <param term='commandText'>Text requested by the neededText parameter.</param>
		/// <seealso class='Exec' />
		public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
		{
			if(neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
			{
                if (commandName == "MKAddin.Connect.AlignOp")
				{
					status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported|vsCommandStatus.vsCommandStatusEnabled;
					return;
				}
                if (commandName == "MKAddin.Connect.ReTab")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
                if (commandName == "MKAddin.Connect.CancelRet")
				{
					status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported|vsCommandStatus.vsCommandStatusEnabled;
					return;
				}
                if (commandName == "MKAddin.Connect.OperatorCompletion")
				{
					status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported|vsCommandStatus.vsCommandStatusEnabled;
					return;
				}
                if (commandName == "MKAddin.Connect.AddCppUnitTestMethod")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
                if (commandName == "MKAddin.Connect.SmartSemicolon")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
                if (commandName == "MKAddin.Connect.SwitchToPreviousDocument")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
                if (commandName == "MKAddin.Connect.AddCppAndH")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
                if (commandName == "MKAddin.Connect.ExtractConstant")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
                if (commandName == "MKAddin.Connect.MakeTupleConstructor")
                {
                    status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                    return;
                }
            }
		}

		/// <summary>Implements the Exec method of the IDTCommandTarget interface. This is called when the command is invoked.</summary>
		/// <param term='commandName'>The name of the command to execute.</param>
		/// <param term='executeOption'>Describes how the command should be run.</param>
		/// <param term='varIn'>Parameters passed from the caller to the command handler.</param>
		/// <param term='varOut'>Parameters passed from the command handler to the caller.</param>
		/// <param term='handled'>Informs the caller if the command was handled or not.</param>
		/// <seealso class='Exec' />
		public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
		{
			handled = false;
			if(executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
			{
                if (commandName == "MKAddin.Connect.AlignOp")
				{
                    AlignOp();
					handled = true;
					return;
				}
                if (commandName == "MKAddin.Connect.ReTab")
                {
                    ReTab();
                    handled = true;
                    return;
                }
                if (commandName == "MKAddin.Connect.CancelRet")
                {
                    CancelRet();
                    handled = true;
                    return;
                }
                if (commandName == "MKAddin.Connect.OperatorCompletion")
                {
                    OperatorCompletion();
                    handled = true;
                    return;
                }
                if (commandName == "MKAddin.Connect.AddCppUnitTestMethod")
                {
                    AddCppUnitTestMethod();
                    handled = true;
                    return;
                }
                if (commandName == "MKAddin.Connect.SmartSemicolon")
                {
                    EndSemicolon();
                    handled = true;
                    return;
                }
                if (commandName == "MKAddin.Connect.SwitchToPreviousDocument")
                {
                    SwitchToPreviousDocument();
                    handled = true;
                    return;
                }
                if (commandName == "MKAddin.Connect.AddCppAndH")
                {
                    AddCppAndHAtOnce();
                    handled = true;
                    return;
                }
                if (commandName == "MKAddin.Connect.ExtractConstant")
                {
                    ExtractConstant();
                    handled = true;
                    return;
                }
                if (commandName == "MKAddin.Connect.MakeTupleConstructor")
                {
                    MakeTupleConstructor();
                    handled = true;
                    return;
                }
            }
		}

        public void AlignOp()
        {
            // TextDocument objTextDoc = (TextDocument)_applicationObject.ActiveDocument.Object("TextDocument"); 
            // EditPoint objEditPoint  = (EditPoint)objTextDoc.StartPoint.CreateEditPoint();
            TextSelection textSelection = (TextSelection)_applicationObject.ActiveDocument.Selection;
            try {
                _applicationObject.UndoContext.Open("AlignOp", true);
                String selectedText = textSelection.Text;
                textSelection.Text = "HOGE" + selectedText + "HIGE";
            } finally {
                _applicationObject.UndoContext.Close();
            }
        }

        public void ReTab()
        {
            TextSelection textSelection = (TextSelection)_applicationObject.ActiveDocument.Selection;
            try
            {
                _applicationObject.UndoContext.Open("ReTab", true);
                textSelection.StartOfLine(vsStartOfLineOptions.vsStartOfLineOptionsFirstColumn, false);
                textSelection.EndOfLine(true);
                String selectedText = textSelection.Text;
                if (!ContainsNonSpaceChar(selectedText))
                {
                    textSelection.Text = "";
                    textSelection.DeleteLeft(1);
                }
                else
                {
                    textSelection.LineUp(false, 1);
                    textSelection.EndOfLine(false);
                }
                textSelection.Text = "\n";
            }
            finally
            {
                _applicationObject.UndoContext.Close();
            }
        }

        public void CancelRet()
        {
            TextSelection textSelection = (TextSelection)_applicationObject.ActiveDocument.Selection;
            try
            {
                _applicationObject.UndoContext.Open("CancelRet", true);
                textSelection.StartOfLine(vsStartOfLineOptions.vsStartOfLineOptionsFirstText, false);
                textSelection.StartOfLine(vsStartOfLineOptions.vsStartOfLineOptionsFirstColumn, true);
                textSelection.Text = "";
                textSelection.DeleteLeft(1);
                textSelection.DeleteWhitespace(vsWhitespaceOptions.vsWhitespaceOptionsHorizontal);
            }
            finally
            {
                _applicationObject.UndoContext.Close();
            }
        }

        private void OperatorCompletion_ForVector(ref TextSelection textSelection, String varName)
        {
            String prevComponent = GetPreviousPhrase(ref textSelection);
            textSelection.Text = "for(int " + varName + " = 0; " + varName + " < int(" + prevComponent + ".size()); " + varName + "++) {\n;\n}";
            TextRanges tr = null;
            textSelection.FindPattern(";", (int)vsFindOptions.vsFindOptionsBackwards, ref tr);
            textSelection.Text = "";
        }

        private void OperatorCompletion_ForIterator(ref TextSelection textSelection, String iteratorType, String reversePrefix, String varName)
        {
            String containerName, typeName;
            GetTypeAndNameForOperatorCompletion(ref textSelection, out typeName, out containerName);
            textSelection.Text = "for(" + typeName + "::" + iteratorType + " " + varName + " = " + containerName + "." + reversePrefix + "begin(); " + varName + " != " + containerName + "." + reversePrefix + "end(); ++" + varName + ") {\n;\n}";
            TextRanges tr = null;
            textSelection.FindPattern(";", (int)vsFindOptions.vsFindOptionsBackwards, ref tr);
            textSelection.Text = "";
        }

        public void OperatorCompletion()
        {
            TextSelection textSelection = (TextSelection)_applicationObject.ActiveDocument.Selection;
            try
            {
                _applicationObject.UndoContext.Open("OperatorCompletion", true);
                textSelection.WordLeft(false, 1);
                TextPoint cursorSave = textSelection.ActivePoint.CreateEditPoint();
                textSelection.WordRight(true, 1);
                String operatorName = textSelection.Text;
                switch(operatorName) {
                    case "foria":
                        OperatorCompletion_ForVector(ref textSelection, "i");
                        break;
                    case "forja":
                        OperatorCompletion_ForVector(ref textSelection, "j");
                        break;
                    case "forka":
                        OperatorCompletion_ForVector(ref textSelection, "k");
                        break;
                    case "forxa":
                        OperatorCompletion_ForVector(ref textSelection, "x");
                        break;
                    case "forya":
                        OperatorCompletion_ForVector(ref textSelection, "y");
                        break;
                    case "forza":
                        OperatorCompletion_ForVector(ref textSelection, "z");
                        break;
                    case "forit":
                        OperatorCompletion_ForIterator(ref textSelection, "iterator", "", "it");
                        break;
                    case "forrit":
                        OperatorCompletion_ForIterator(ref textSelection, "reverse_iterator", "r", "it");
                        break;
                    case "forcit":
                        OperatorCompletion_ForIterator(ref textSelection, "const_iterator", "", "cit");
                        break;
                    case "forcrit":
                        OperatorCompletion_ForIterator(ref textSelection, "const_reverse_iterator", "r", "cit");
                        break;
                    case "forjt":
                        OperatorCompletion_ForIterator(ref textSelection, "iterator", "", "jt");
                        break;
                    case "forrjt":
                        OperatorCompletion_ForIterator(ref textSelection, "reverse_iterator", "r", "jt");
                        break;
                    case "forcjt":
                        OperatorCompletion_ForIterator(ref textSelection, "const_iterator", "", "cjt");
                        break;
                    case "forcrjt":
                        OperatorCompletion_ForIterator(ref textSelection, "const_reverse_iterator", "r", "cjt");
                        break;
                    case "forkt":
                        OperatorCompletion_ForIterator(ref textSelection, "iterator", "", "kt");
                        break;
                    case "forrkt":
                        OperatorCompletion_ForIterator(ref textSelection, "reverse_iterator", "r", "kt");
                        break;
                    case "forckt":
                        OperatorCompletion_ForIterator(ref textSelection, "const_iterator", "", "ckt");
                        break;
                    case "forcrkt":
                        OperatorCompletion_ForIterator(ref textSelection, "const_reverse_iterator", "r", "ckt");
                        break;
                }
            }
            finally
            {
                _applicationObject.UndoContext.Close();
            }
        }

        private void AddCppUnitTestMethod()
        {
            String testMethodName = "";
            DialogResult res = InputBox("Test Case Name", "Input test case name", ref testMethodName);
            if (res != DialogResult.OK || testMethodName == "")
                return;
            TextSelection textSelection = (TextSelection)_applicationObject.ActiveDocument.Selection;
            TextRanges dummyTs = null;
            try
            {
                _applicationObject.UndoContext.Open("AddCppUnitTestMethod", true);
                textSelection.StartOfDocument(false);
                if (!textSelection.FindPattern("//CUPPA:suite=-", (int)vsFindOptions.vsFindOptionsNone, ref dummyTs))
                {
                    MessageBox.Show("'//CUPPA:suite=-'コメントがソース中に見つかりません（・ω・｀）", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                textSelection.StartOfDocument(false);
                if (!textSelection.FindPattern("//CUPPA:decl=-", (int)vsFindOptions.vsFindOptionsNone, ref dummyTs))
                {
                    MessageBox.Show("'//CUPPA:decl=-'コメントがソース中に見つかりません（・ω・｀）", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                textSelection.StartOfDocument(false);
                if (!textSelection.FindPattern("//CUPPA:impl=-", (int)vsFindOptions.vsFindOptionsNone, ref dummyTs))
                {
                    MessageBox.Show("'//CUPPA:impl=-'コメントがソース中に見つかりません（・ω・｀）", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                textSelection.StartOfDocument(false);
                textSelection.FindPattern("//CUPPA:suite=-", (int)vsFindOptions.vsFindOptionsNone, ref dummyTs);
                textSelection.LineUp(false, 1);
                textSelection.EndOfLine(false);
                textSelection.Text = "\nCPPUNIT_TEST(test" + testMethodName + ");";
                textSelection.FindPattern("//CUPPA:decl=-", (int)vsFindOptions.vsFindOptionsNone, ref dummyTs);
                textSelection.LineUp(false, 1);
                textSelection.EndOfLine(false);
                textSelection.Text =
                      "\nvoid test" + testMethodName + "() {\n"
                    + "CPPUNIT_FAIL(\"no implementation\");\n"
                    + "}";
                textSelection.FindPattern("CPPUNIT_FAIL(\"no implementation\");", (int)vsFindOptions.vsFindOptionsBackwards, ref dummyTs);
            }
            finally
            {
                _applicationObject.UndoContext.Close();
            }
        }

        private void EndSemicolon()
        {
            TextSelection textSelection = (TextSelection)_applicationObject.ActiveDocument.Selection;
            try
            {
                _applicationObject.UndoContext.Open("EndSemicolon", true);
                textSelection.EndOfLine(false);
                textSelection.Text = ";";
            }
            finally
            {
                _applicationObject.UndoContext.Close();
            }
        }

        private void SwitchToPreviousDocument()
        {
            int documentCounts = _applicationObject.Windows.Count;
            if(documentCounts < 2)
                return;
            String currentDocumentName = _applicationObject.ActiveDocument.Name;
            String currentDocumentNameBase;
            int cdi = currentDocumentName.IndexOf('.');
            if(cdi == -1) {
                currentDocumentNameBase = currentDocumentName;
            } else {
                currentDocumentNameBase = currentDocumentName.Substring(0, cdi);
            }

            _applicationObject.ExecuteCommand("Window.PreviousDocumentWindow", "");

            String secondDocumentName = _applicationObject.ActiveDocument.Name;
            String secondDocumentNameBase;
            int sdi = secondDocumentName.IndexOf('.');
            if(sdi == -1) {
                secondDocumentNameBase = secondDocumentName;
            } else {
                secondDocumentNameBase = secondDocumentName.Substring(0, sdi);
            }
            if(currentDocumentName == secondDocumentName && 3 <= documentCounts) {
                _applicationObject.ExecuteCommand("Window.PreviousDocumentWindow", "");
            }
        }

        private void AddCppAndHAtOnce()
        {
            String baseFileName = "";
            DialogResult res = InputBox("Base file name", "Input the base file name for .cpp/.h", ref baseFileName);
            if (res != DialogResult.OK || baseFileName == "")
                return;
            Solution2 activeSolution = (Solution2)_applicationObject.Solution;
            if (activeSolution.Count < 1)
            {
                MessageBox.Show("Project が見つかりません", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Project project = (Project)activeSolution.Item(1); // 1-origin. When I gave 0, an exception was thrown, it seems.
            String projectDirectory = Path.GetDirectoryName(project.FullName);
            String cppFullPath = Path.Combine(projectDirectory, baseFileName + ".cpp");
            String cppText =
                "/**\n * @file " + baseFileName + ".cpp\n * @brief\n */\n\n" +
                "#include\"stdafx.h\"\n" +
                "#include\"" + baseFileName + ".h\"\n\n" +
                "using namespace std;\n\n";
            if(!createStubFile(cppFullPath, cppText)) {
                MessageBox.Show(cppFullPath + " の生成に失敗しました。", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            String headerFullPath = Path.Combine(projectDirectory, baseFileName + ".h");
            String headerText =
                "/**\n * @file " + baseFileName + ".h\n * @brief\n */\n\n" +
                "#pragma once\n" +
                "#ifndef _HEADER_" + baseFileName.ToUpper() + "\n" +
                "#define _HEADER_" + baseFileName.ToUpper() + "\n\n" +
                "#endif // #ifndef _HEADER_" + baseFileName.ToUpper() + "\n";
            if(!createStubFile(headerFullPath, headerText)) {
                File.Delete(cppFullPath);
                MessageBox.Show(headerFullPath + " の生成に失敗しました。", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            project.ProjectItems.AddFromFile(cppFullPath);
            project.ProjectItems.AddFromFile(headerFullPath);
        }

        private bool createStubFile(String fileName, String text)
        {
            try
            {
                StreamWriter sw = File.CreateText(fileName);
                sw.Write(text);
                sw.Close();
            }
            catch(UnauthorizedAccessException e) {
                return false;
            }
            catch(DirectoryNotFoundException e) {
                return false;
            }
            catch(ArgumentException e) {
                return false;
            }
            catch(NotSupportedException e) {
                return false;
            }
            return true;
        }

        private void ExtractConstant()
        {
            TextSelection textSelection = (TextSelection)_applicationObject.ActiveDocument.Selection;
            try
            {
                _applicationObject.UndoContext.Open("ExtractConstant", true);
                String expression = textSelection.Text;
                if (expression.Length < 1) return;
                String constVarName = null;
                DialogResult res = InputBox("Extract Constant", "Input the const variable name", ref constVarName);
                if (res != DialogResult.OK || constVarName == "")
                    return;
                textSelection.Text = constVarName;
                textSelection.LineUp(false, 1);
                textSelection.EndOfLine(false);
                textSelection.Text = "\nconst # " + constVarName + " = " + expression + ";";
                TextRanges ts = null;
                textSelection.FindPattern("#", (int)vsFindOptions.vsFindOptionsBackwards, ref ts);
                textSelection.Text = "";
            }
            finally
            {
                _applicationObject.UndoContext.Close();
            }
        }

        const String GUARD_STRING = "###GUARD###";

        private String MakeItArgument(String s)
        {
            String b = SPTrim(s);
            while(true) {
                bool unmodified = true;
                if (b.IndexOf("unsigned ") == 0)
                {
                    b = b.Substring(9);
                    unmodified = false;
                }
                if (b.IndexOf("const ") == 0)
                {
                    b = b.Substring(6);
                    unmodified = false;
                }
                if (b.IndexOf("volatile ") == 0)
                {
                    b = b.Substring(9);
                    unmodified = false;
                }
                if (unmodified) break;
                b = SPTrim(b);
            }
            while (0 < b.Length && b[0] == '_') { b = b.Substring(1); }
            while (0 < b.Length && Char.IsDigit(b[b.Length - 1])) { b = b.Substring(0, b.Length - 1); }
            if (0 < b.Length && b[0] == 'u') { b = b.Substring(1); }
            switch(b) {
                case "char":
                case "short":
                case "int":
                case "long":
                case "long long":
                case "float":
                case "double":
                case "long double":
                    return s;
            }
            if (0 < b.Length && b[b.Length - 1] == '*')
                return s;
            if(s.IndexOf("const ") != -1)
                return s + "&";
            return "const " + s + "&";
        }
        private readonly char[] Spaces = {' ', '\t', '\r', '\n'};
        private void MakeTupleConstructor()
        {
            TextSelection textSelection = (TextSelection)_applicationObject.ActiveDocument.Selection;
            try
            {
                _applicationObject.UndoContext.Open("MakeTupleConstructor", true);
                textSelection.Text = GUARD_STRING;
                TextRanges matchRanges = null;
                if(!textSelection.FindPattern("(class|struct)[ \t]+([_A-Za-z][_A-Za-z0-9]*)", (int)vsFindOptions.vsFindOptionsBackwards | (int)vsFindOptions.vsFindOptionsRegularExpression, ref matchRanges)) {
                    MessageBox.Show("Could not find class/struct definition.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _applicationObject.UndoContext.SetAborted();
                    return;
                }
                textSelection.CharLeft(false, 1);
                textSelection.WordRight(false, 1);
                textSelection.WordRight(true, 1);
                String className = textSelection.Text;
                List<String> varDefinitions = new List<String>();
                List<String> varNames = new List<String>();

                while (true)
                {
                    textSelection.LineDown(false, 1);
                    textSelection.StartOfLine(vsStartOfLineOptions.vsStartOfLineOptionsFirstColumn, false);
                    textSelection.EndOfLine(true);
                    String lineStr = textSelection.Text;
                    if(lineStr.IndexOf(GUARD_STRING) != -1)
                        break;
                    if(lineStr.IndexOf(':') != -1)
                        continue;
                    if (lineStr.IndexOf("static ") != -1)
                        continue;
                    int semiPos = lineStr.IndexOf(';');
                    if (semiPos == -1)
                        continue;
                    String defStr = SPTrim(lineStr.Substring(0, semiPos));
                    MessageBox.Show("DEF: " + defStr);
                    int lastSpacePos = defStr.LastIndexOfAny(Spaces);
                    if (lastSpacePos == -1)
                        continue;
                    String varName = defStr.Substring(lastSpacePos + 1);
                    String definitionStr = SPTrim(defStr.Substring(0, lastSpacePos));
                    MessageBox.Show(String.Format("Def={0}, Var={1}", definitionStr, varName));
                    varNames.Add(varName);
                    varDefinitions.Add(MakeItArgument(definitionStr));
                }
                textSelection.Text = "inline " + className + "(";
                for (int i = 0; i < varDefinitions.Count; i++)
                {
                    if (0 < i) { textSelection.Text = ", "; }
                    textSelection.Text = varDefinitions[i] + " " + varNames[i];
                }
                textSelection.Text = ") : ";
                for (int i = 0; i < varNames.Count; i++)
                {
                    if (0 < i) { textSelection.Text = ", "; }
                    textSelection.Text = varNames[i] + "(" + varNames[i] + ")";
                }
                textSelection.Text = " {}\ninline " + className + "() {}\n";
            }
            finally
            {
                _applicationObject.UndoContext.Close();
            }
        }

		private DTE2 _applicationObject;
		private AddIn _addInInstance;
	}
}
