using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace The_Search_Dude.Controls.ValueConverters
{
    /* 
     * This script is responsible by the function of conversion of a BOOL value to VISIBILITY enum value. This is useful to do conversion
     * between BOOL and VISIBILITY, to make possible that exposed BOOL "DependencyProperty" of UserControls, can be binded to "Visibility" properties
     * of elements, inside a UserControl.
     * 
     * To use this Value Converter, first, make sure that the Script of the User Control have a exposed BOOL "DependencyProperty" to be used as
     * Source and to be changed when the User Control be used by the dev.
     * 
     * Then, go to XML of the User Control, and add the "xmlns:valueConvertersLocal="clr-namespace:The_Search_Dude.Controls.ValueConverters" to
     * namespaces of the root "UserControl" tag. Replace the content after "clr-namespace:" with the exact namespace of this Value Converter script.
     * 
     * Add the code below, to resources of the User Control
     * <UserControl.Resources>
     *  <valueConvertersLocal:UserControl_BoolToVisibilityConverter x:Key="UserControl_BoolToVisibilityConverter" />
     * </UserControl.Resources>
     * 
     * Now, choose a element inside the User Control, and add the Binding code below to the Visibility parameter of this element...
     * Visibility="{Binding BoolDependencyPropertyName, Converter={StaticResource UserControl_BoolToVisibilityConverter}}"
     * Replace the "BoolDependencyPropertyName" with the name of the BOOL "DependencyProperty" to be used as source for this Value Converter.
     * 
     * It's all.
    */

    public class UserControl_BoolToVisibilityConverter : IValueConverter
    {
        //Public method to do conversion from BOOL -> VISIBILITY

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //If the value is not bool, cancel here...
            if (value is bool == false)
                return Visibility.Visible;



            //Prepare the value to return
            Visibility toReturn = Visibility.Visible;

            //If is "true"
            if ((bool)value == true)
                toReturn = Visibility.Visible;
            //If is "false"
            if ((bool)value == false)
                toReturn = Visibility.Collapsed;

            //Return the value
            return toReturn;
        }

        //Public method to do conversion from VISIBILITY -> BOOL

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //If the value is not Visibility, cancel here...
            if (value is Visibility == false)
                return true;



            //Prepare the value to return
            bool toReturn = true;

            //If is "Visible"
            if ((Visibility)value == Visibility.Visible)
                toReturn = true;
            //If is "Collapsed"
            if ((Visibility)value == Visibility.Collapsed)
                toReturn = false;
            //If is "Hidden"
            if ((Visibility)value == Visibility.Hidden)
                toReturn = false;

            //Return the value
            return toReturn;
        }
    }
}
