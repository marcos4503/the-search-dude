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
     * This class holds and handles the central logic of Key Render
    */

    public partial class WindowKeyRender : Window
    {
        //Core methods

        public WindowKeyRender()
        {
            //Initialize the Window
            InitializeComponent();
        }
    
        //Public auxiliar method

        public void SetKeyText(string keyTxt)
        {
            //Set the text in the key
            this.keyTxt.Text = keyTxt;
        }
    }
}
