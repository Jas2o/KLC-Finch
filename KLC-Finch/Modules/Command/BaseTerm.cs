using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace KLC_Finch {
    public class BaseTerm {

        public bool SHOW_DEBUG_TEXT = false;

        public RichTextBox RichText;
        private Color DefaultForeColor, DefaultBackColor, ForeColor, BackColor;
        private FontStyle FontStyle;
        private FontWeight FontWeight;
        private int posVert;
        private int posHori;
        private string buffer;

        //http://ascii-table.com/ansi-escape-sequences-vt-100.php
        public BaseTerm(RichTextBox richBox) {
            RichText = richBox;
            DefaultForeColor = ForeColor = ((SolidColorBrush)RichText.Foreground).Color;
            DefaultBackColor = BackColor = ((SolidColorBrush)RichText.Background).Color;
            FontStyle = FontStyles.Normal;
            FontWeight = FontWeights.Normal;

            posVert = 0;
            posHori = 0;
        }

        public void Append(string input) {
            buffer = "";
            if (SHOW_DEBUG_TEXT)
                RichText.AppendText("START", Colors.Black, Colors.White);

            bool escape = false;
            for (int pos = 0; pos < input.Length; pos++) {
                if (escape) {
                    escape = false;
                    if (input[pos] == '[') {
                        //Need to look ahead to the nearest of escSquare
                        string collected = "";
                        bool lookAhead = true;
                        while (lookAhead) {
                            char lookingAt = input[++pos];
                            lookAhead = false;
                            if (lookingAt == 'J') {
                                //Different types of screen clears
                            } else if (lookingAt == 'h') {
                                //Make Cursor visible
                            } else if (lookingAt == 'l') {
                                //Make Cursor invisible
                            } else if (lookingAt == 'H') {
                                //Move cursor to position.
                                if (SHOW_DEBUG_TEXT)
                                    RichText.AppendText("Esc[" + collected + "H", Colors.Black, Colors.White);
                                string[] codes = collected.Split(';');
                                if (codes.Length > 0) {
                                    int vert = int.Parse(codes[0]);
                                    if (posVert < vert) {
                                        if (SHOW_DEBUG_TEXT)
                                            RichText.AppendText("[N]", Colors.Red, Colors.Yellow);
                                        RichText.AppendText(Environment.NewLine);
                                    }
                                    posVert = vert;
                                }
                                if (codes.Length > 1)
                                    posHori = int.Parse(codes[1]);
                                //richBox.AppendText(Environment.NewLine); //Assumption
                            } else if (lookingAt == 'm') {
                                HandleTextMode(collected.Split(';'));
                            } else if (char.IsLetter(lookingAt)) {
                                if (SHOW_DEBUG_TEXT)
                                    RichText.AppendText("Esc[" + collected + lookingAt, Colors.Red, Colors.Yellow);
                                Console.WriteLine("Unknown lookingAt: " + lookingAt);
                            } else {
                                collected = collected + lookingAt;
                                lookAhead = true;
                            }
                        }
                    } else {
                        char next = input[pos];
                        switch (next) {
                            case 'D':
                                //Scroll up one line
                                if (SHOW_DEBUG_TEXT)
                                    RichText.AppendText("EscD", Colors.Black, Colors.White);
                                RichText.AppendText(Environment.NewLine);
                                break;
                            default:
                                if (SHOW_DEBUG_TEXT)
                                    RichText.AppendText("Esc"+next, Colors.Red, Colors.Yellow);
                                Console.WriteLine("Unknown next: " + next);
                                break;
                        }
                    }
                } else {
                    if (input[pos] == 0x001b) {
                        FlushBuffer();
                        escape = true;
                    } else if (input[pos] == '\r') {
                        //richBox.AppendText("[R]", Colors.Black, Colors.White);
                    } else if (input[pos] == '\n') {
                        FlushBuffer();
                        if (SHOW_DEBUG_TEXT)
                            RichText.AppendText("[N]", Colors.Black, Colors.White, posVert, posHori);
                        RichText.AppendText(Environment.NewLine);
                    } else {
                        buffer += input[pos];
                    }
                }
            }

            FlushBuffer();
        }

        private void FlushBuffer() {
            RichText.AppendText(buffer, ForeColor, BackColor);
            buffer = "";
        }

        #region Text Modes
        //Italic and Bold changed due to WPF.

        Dictionary<string, Color> dictionaryForeColor = new Dictionary<string, Color>() {
            {"30", Colors.Black },
            {"31", Colors.Red },
            {"32", Colors.Green },
            {"33", Colors.Yellow },
            {"34", Colors.Blue },
            {"35", Colors.Magenta },
            {"36", Colors.Cyan },
            {"37", Colors.White },
        };

        Dictionary<string, Color> dictionaryBackColor = new Dictionary<string, Color>() {
            {"40", Colors.Black },
            {"41", Colors.Red },
            {"42", Colors.Green },
            {"43", Colors.Yellow },
            {"44", Colors.Blue },
            {"45", Colors.Magenta },
            {"46", Colors.Cyan },
            {"47", Colors.White },
        };

        private void HandleTextMode(string[] codes) {
            foreach (string code in codes) {
                if (code == "" || code == "0") {
                    //Reset/normal
                    ForeColor = DefaultForeColor;
                    BackColor = DefaultBackColor;
                    FontStyle = FontStyles.Normal;
                    FontWeight = FontWeights.Normal;
                } else if (code == "1") { //Bold
                    FontWeight = FontWeights.Bold;
                } else if (code == "3") { //Italic
                    FontStyle = FontStyles.Italic;
                } else if (dictionaryForeColor.ContainsKey(code)) {
                    ForeColor = dictionaryForeColor[code];
                } else if (code == "39") {
                    ForeColor = DefaultForeColor;
                } else if (dictionaryBackColor.ContainsKey(code)) {
                    BackColor = dictionaryBackColor[code];
                } else if(code == "49") {
                    BackColor = DefaultBackColor;
                } else {
                    Console.WriteLine("Unknown color mode: " + code);
                }
            }

            if (SHOW_DEBUG_TEXT)
                RichText.AppendText("[T:" + string.Join(";", codes) + "]", Colors.Red, Colors.Yellow);
        }
        #endregion

    }
}
