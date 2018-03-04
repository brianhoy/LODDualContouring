using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public static class UConsole {
	public static Console ConsoleObject;
	public delegate void Command(string[] args);

	public static void Print(string msg) {
		ConsoleObject.PrintString(msg);
	}
	public static void AddCommand(string commandName, Command command) {
		ConsoleObject.AddCommand(commandName, command);
	}
}

public class Console : MonoBehaviour {
    public InputField InputText;
    public Text ConsoleOutput;

    private string Output;
    private string[] Lines;
    private int CurrentLine = 0;
    private int MaxLines = 10;
    private string CurrentText;
	private Dictionary<string, UConsole.Command> commands;

    public void Start() {
        Lines = new string[MaxLines];
        for(int i = 0; i < MaxLines; i++) {
            Lines[i] = "";
        }
		UConsole.ConsoleObject = this.GetComponent<Console>();
		commands = new Dictionary<string, UConsole.Command>();
    }

	public void AddCommand(string commandName, UConsole.Command command) {
		commands.Add(commandName, command);
	}

    public void Update() {
        if(Input.GetKeyDown(KeyCode.Return)) {
            EnterPressed();
        }
    }

    public void EnterPressed() {
        PrintString(" > " + InputText.text);
        ProcessCommand(InputText.text);
		InputText.text = "";
    }

    public delegate void RegenerateMesh(int n);
    private RegenerateMesh regen;
    public void SetRegenerateFn(RegenerateMesh a) {
        regen = a;
    }

    public void ProcessCommand(string commandStr) {
        string[] tokens = commandStr.Split(' ');
        string command = tokens[0];
		List<string> tokensList = new List<string>(tokens);
		tokensList.RemoveAt(0);
		if(commands.ContainsKey(command)) {
			commands[command](tokensList.ToArray());
		}
		else {
			PrintString("ERROR: Invalid command");
		}
    }
    public void PrintString(string str) {
        Lines[CurrentLine] = str;
        CurrentLine = (CurrentLine + 1) % MaxLines;
        GenerateOutput();
    }

    private void GenerateOutput() {
        Output = "";
        for(int i = 0; i < MaxLines; i++) {
            Output += Lines[(i + CurrentLine) % MaxLines] + "\n";
        }
        ConsoleOutput.text = Output;
    }

    public void InputChanged(string useless) {
        CurrentText = InputText.text;
    }
}