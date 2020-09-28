using StellarisSQLiteModManager.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace StellarisSQLiteModManager.Models
{
    public class ModInPlayset : INotifyPropertyChanged
    {
        public ModInPlayset(Mod fromMod, Playset fromPlayset, long startIndex) : this(fromMod, fromPlayset, startIndex, false, false)
        {

        }

        public ModInPlayset(Mod fromMod, Playset fromPlayset, long startIndex, bool initiallyActive, bool exists)
        {
            TargetMod = fromMod;
            TargetPlayset = fromPlayset;
            this.index = startIndex;
            this.active = initiallyActive;
            this.PropertyChanged += UpdateActive;
            this.Exists = exists;
        }

        private bool Exists;

        public Mod TargetMod { get; }

        public Playset TargetPlayset { get; }

        public string ModName
        {
            get
            {
                return TargetMod.Modname;
            }
        }

        private long index;
        public long Index
        {
            get
            {
                return index;
            }
            set
            {
                if (value != index)
                {
                    index = value;
                    NotifyPropertyChanged();
                }
            }
        }

        //3333333334 = 10 chars
        public string DatabaseIndex
        {
            get
            {
                return LongIndexToDbIndex(Index);
            }
        }

        private bool active;
        public bool Active
        {
            get
            {
                return active;
            }
            set
            {
                if (value != active)
                {
                    active = value;
                    NotifyPropertyChanged();
                }
            }
        }

        #region Indexing

        const char MagicPadding = '3';
        const long MagicModulo = 36; //10 digits + 26 letters
        const long MagicFirstDigitOffset = 3;
        const long MagicOffset = MagicFirstDigitOffset + 1; //DB is 1-Indexed (3 is 0-buffer, but values start at 3..4 not 3..3)
        const int ASCII_num = 0x30;
        const int ASCII_lower = 0x61;
        static readonly char[] VALUE_TO_CHAR_LUT = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
        static readonly Dictionary<char, int> CHAR_TO_VALUE_LUT = new Dictionary<char, int>() { { '0',  0 }, { '1',  1 }, { '2',  2 }, { '3',  3 }, { '4',  4 }, { '5',  5 }, { '6',  6 }, { '7',  7 }, { '8',  8 }, { '9',  9 },
                                                                                          { 'a', 10 }, { 'b', 11 }, { 'c', 12 }, { 'd', 13 }, { 'e', 14 }, { 'f', 15 }, { 'g', 16 }, { 'h', 17 }, { 'i', 18 }, { 'j', 19 },
                                                                                          { 'k', 20 }, { 'l', 21 }, { 'm', 22 }, { 'n', 23 }, { 'o', 24 }, { 'p', 25 }, { 'q', 26 }, { 'r', 27 }, { 's', 28 }, { 't', 29 },
                                                                                          { 'u', 30 }, { 'v', 31 }, { 'w', 32 }, { 'x', 33 }, { 'y', 34 }, { 'z', 35 } };

        public static string LongIndexToDbIndex(long toConvert)
        {
            long tempIndex = toConvert + 1; //Long index is 0-based, but calculation uses 1-based for easier usage, since values start at 3...34 not 3...33
            char[] resultArray = new char[10];

            for(int round = 9; round >= 0; round--)
            {
                tempIndex += MagicFirstDigitOffset;
                resultArray[round] = VALUE_TO_CHAR_LUT[tempIndex % MagicModulo];
                tempIndex /= MagicModulo;
            }

            return new string(resultArray);
        }

        public static long DBIndexToLongIndex(string toConvert)
        {
            string cut = toConvert.Substring(0, 10); //Ignore all chars after 10th. They have some deeper ordering sense, which is not important to us for now
            long result = 0;
            string temp = "";
            bool paddingEnded = false;

            foreach(char c in cut)
            {
                if(!paddingEnded && c == MagicPadding)
                {
                    continue;
                }
                paddingEnded = true;
                temp += c;
            }

            char[] chars = temp.ToCharArray();
            for(int exponent = 0; exponent < chars.Length; exponent++)
            {
                char current = chars[chars.Length - exponent - 1];
                if (exponent == chars.Length - 1)
                {
                    result += (long)Math.Pow(MagicModulo, exponent) * (CHAR_TO_VALUE_LUT[current] - MagicFirstDigitOffset);
                }
                else
                {
                    result += (long)Math.Pow(MagicModulo, exponent) * CHAR_TO_VALUE_LUT[current];
                }

                //Clean Digit Offset Overhead for last iteration
                if(exponent > 0)
                {
                    result -= 3 * (long)Math.Pow(MagicModulo, exponent - 1);
                }
            }
            result -= 1; //1-Indexed DB to 0-Indexed Software

            Debug.WriteLine("Computed " + cut + " to temp " + temp + " and to result " + result + " which reverses to " + LongIndexToDbIndex(result));            

            return result;
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateActive(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(Active))
            {
                Debug.WriteLine("Updated MIP Active!");
                DatabaseFunctions.Singleton.UpdateModInPlaysetEnabled(this);
            }
            else if (args.PropertyName == nameof(Index))
            {
                Debug.WriteLine("Updated MIP Index of " + TargetMod.Modname + " to " + Index);
                DatabaseFunctions.Singleton.UpdateModInPlaysetIndex(this);
            }
        }

        public void RemoveThis()
        {
            Debug.WriteLineIf(Exists, "Removing " + TargetMod.Modname);
            if (Exists)
            {
                DatabaseFunctions.Singleton.RemoveModFromPlayset(this);
                Exists = false;
            }
        }

        public void AddThis()
        {
            Debug.WriteLineIf(!Exists, "Adding " + TargetMod.Modname);
            if (!Exists)
            {
                DatabaseFunctions.Singleton.AddModToPlayset(this);
                Exists = true;
            }
        }
    }
}
