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
     * This class handles the logic of the Position Picker Window
    */

    public partial class WindowPositionPicker : Window
    {
        //Cache variables
        private bool isHoldingMouseClick = false;

        //Core methods

        public WindowPositionPicker(Color pickerMainColor, Color pickerSecondaryColor, string pickerDescription)
        {
            //Initialize the Window
            InitializeComponent();

            //Setup the movement of this Window
            SetupThisWindowMovement();

            //Display the picker color
            pickerBorder.BorderBrush = new SolidColorBrush(pickerMainColor);
            pickerBorder.Background = new SolidColorBrush(Color.FromArgb(25, pickerMainColor.R, pickerMainColor.G, pickerMainColor.B));
            arrowRect.Fill = new SolidColorBrush(pickerMainColor);
            pickerSubBorder.Background = new SolidColorBrush(pickerSecondaryColor);

            //Display the picker text
            pickerText.Text = pickerDescription;
        }

        //Private auxiliar methods

        private void SetupThisWindowMovement()
        {
            //Prepare the click detection
            this.MouseDown += (s, e) => {
                //Inform that is clicking
                isHoldingMouseClick = true;
            };
            this.MouseUp += (s, e) => {
                //Inform that is not clicking
                isHoldingMouseClick = false;
            };
            this.MouseLeave += (s, e) => {
                //Inform that is not clicking
                isHoldingMouseClick = false;
            };

            //Setup the Window movement based on mouse, if clicking on Window
            this.MouseMove += (s, e) => {
                //If not clicking, cancel here
                if (isHoldingMouseClick == false)
                    return;

                //Get the current mouse position in screen coordinates
                Point screenPoint = Mouse.GetPosition(this); // Get position relative to the window
                Point absoluteScreenPoint = PointToScreen(screenPoint); // Convert to screen coordinates

                //Adjust for the window's size to center it on the cursor
                //double windowLeft = absoluteScreenPoint.X - (this.ActualWidth / 2);
                //double windowTop = absoluteScreenPoint.Y - (this.ActualHeight / 2);
                double windowLeft = absoluteScreenPoint.X - 21.0f;
                double windowTop = absoluteScreenPoint.Y - 20.0f;

                //Set the window's position
                this.Left = windowLeft;
                this.Top = windowTop;
            };
        }
    }
}
