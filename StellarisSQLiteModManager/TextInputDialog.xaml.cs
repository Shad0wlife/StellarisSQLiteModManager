using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace StellarisSQLiteModManager
{
    /// <summary>
    /// Interaktionslogik für TextInputDialog.xaml
    /// </summary>
    public partial class TextInputDialog : Window, INotifyPropertyChanged
    {
        public TextInputDialog(string title, string question) : this(title, question, "")
        {

        }

        public TextInputDialog(string title, string question, string defaultAnswer)
        {
            Title = title;
            QuestionText = question;
            DataContext = this;
            ResultText = defaultAnswer;

            InitializeComponent();
        }

        public string QuestionText { get; }

        private string resultText = "";
        public string ResultText
        {
            get
            {
                return resultText;
            }
            set
            {
                if(value != resultText)
                {
                    resultText = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(ValueValid));
                }
            }
        }

        public bool ValueValid { 
            get
            {
                if(ResultText != "")
                {
                    return true;
                }
                return false;
            } 
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }



        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
