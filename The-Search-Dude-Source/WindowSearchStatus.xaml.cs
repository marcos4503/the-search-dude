using System;
using System.Collections.Generic;
using System.Linq;
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

namespace The_Search_Dude
{
    /*
     * This class holds and handles the logic of Search Status Window
    */

    public partial class WindowSearchStatus : Window
    {
        //Core methods

        public WindowSearchStatus()
        {
            //Initialize the Window
            InitializeComponent();
        }
    
        //Public methods

        public void SetProgressValue(int value, int maxValue)
        {
            //Set progressbar value
            if (value == -1 && maxValue == -1)
            {
                this.progressBar.Maximum = 1;
                this.progressBar.Value = 1;
                this.progressBar.IsIndeterminate = true;
            }
            if (value != -1 && maxValue != -1)
            {
                this.progressBar.Maximum = maxValue;
                this.progressBar.Value = value;
                this.progressBar.IsIndeterminate = false;
            }
        }
    
        public void SetSearchCountText(int current, int max)
        {
            //Set the search counter text
            searchCountTxt.Text = (current + "/"  + max);
        }

        public void SetRemainingTimeText(int minutes, int seconds)
        {
            //Prepare the strings
            string minuteStr = "";
            string secondStr = "";

            //Create the minute string
            if (minutes < 10)
                minuteStr = ("0" + minutes);
            if (minutes >= 10)
                minuteStr = minutes.ToString();
            if (minutes == -1)
                minuteStr = "--";
            //Create the second string
            if (seconds < 10)
                secondStr = ("0" + seconds);
            if (seconds >= 10)
                secondStr = seconds.ToString();
            if (seconds == -1)
                secondStr = "--";

            //Set the remaining timer text
            remainingTimeTxt.Text = (minuteStr + ":" + secondStr);

            //If the time is zero, show that is ending soon
            if (minutes == 0 && seconds == 0)
                remainingTimeTxt.Text = "Finishing Soon!";
        }
        
        public void SetCustomStatus(string statusText)
        {
            //Set the custom status text
            customStatus.Text = statusText;
        }
    }
}
