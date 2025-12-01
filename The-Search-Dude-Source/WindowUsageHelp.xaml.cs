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
     * This class holds and handles the logic of help window
    */

    public partial class WindowUsageHelp : Window
    {
        //Private variables
        private bool isWindowCloseable = true;
        private bool isHelpExpanded = false;

        //Core methods

        public WindowUsageHelp()
        {
            //Initialize the Window
            InitializeComponent();

            //Override the closing, if can't close
            this.Closing += (s, e) => {
                //If can't close, warn
                if (isWindowCloseable == false)
                {
                    e.Cancel = true;
                    MessageBoxResult dialogResult = MessageBox.Show("Close the main Window instead.", "Error", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            //Prepare the Expansion toggle button
            helpScroll.Visibility = Visibility.Collapsed;
            toggleExpansionBtn.Content = "Expand Help";
            toggleExpansionBtn.Click += (s, e) => { ToggleTheWindowExpansion(); };
        }

        private void ToggleTheWindowExpansion()
        {
            //If is not expanded
            if (isHelpExpanded == false)
            {
                //Change button text
                toggleExpansionBtn.Content = "Collapse Help";
                //Expand
                helpScroll.Visibility = Visibility.Visible;

                //Inform that is expanded
                isHelpExpanded = true;
                //Cancel here...
                return;
            }

            //If is expanded
            if (isHelpExpanded == true)
            {
                //Change button text
                toggleExpansionBtn.Content = "Expand Help";
                //Collapse
                helpScroll.Visibility = Visibility.Collapsed;

                //Inform that is not expanded
                isHelpExpanded = false;
                //Cancel here...
                return;
            }
        }

        //Public auxiliar methods

        public void SetCloseable(bool closeable)
        {
            //Set the coseability
            this.isWindowCloseable = closeable;
        }
    }
}
