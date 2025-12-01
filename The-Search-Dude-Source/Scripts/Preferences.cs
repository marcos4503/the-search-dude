using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace The_Search_Dude.Scripts
{
    /*
     * This class manage the load and save of program settings
    */

    public class Preferences
    {
        //Classes of script
        public class LoadedData
        {
            //*** Data to be saved ***//

            public SaveInfo[] saveInfo = new SaveInfo[0];

            public string searchEngineUrlToUse = "duckduckgo.com";
            public int delayToFirstSearch = 15;
            public int countOfSubsequentSearches = 35;
            public int[] delayOfSubsequentSearches = new int[] { 7, 11 };
            public int[] doPauseForEachXSearches = new int[] { 8, 12 };
            public int[] durationForNeededPauses = new int[] { 150, 280 };
            public int simulatedInputsVolume = 80;
            public bool stopSearchingOnMouseMove = true;

            public int currentSelectedControlScheme = 1;
            public int[] scheme1_urlBarClickPoint = new int[] { 50, 50 };
            public int[] scheme1_seHomePageInSearchBarPoint = new int[] { 205, 50 };
            public int[] scheme1_seHomePageOutSearchBarPoint = new int[] { 424, 50 };
            public int[] scheme1_seResultPageInSearchBarPoint = new int[] { 50, 126 };
            public int[] scheme1_seResultPageOutSearchBarPoint = new int[] { 269, 126 };
            public int[] scheme1_seResultPageLogoPoint = new int[] { 498, 126 };
            public int[] scheme2_urlBarClickPoint = new int[] { 50, 50 };
            public int[] scheme2_seHomePageInSearchBarPoint = new int[] { 205, 50 };
            public int[] scheme2_seHomePageOutSearchBarPoint = new int[] { 424, 50 };
            public int[] scheme2_seResultPageInSearchBarPoint = new int[] { 50, 126 };
            public int[] scheme2_seResultPageOutSearchBarPoint = new int[] { 269, 126 };
            public int[] scheme2_seResultPageLogoPoint = new int[] { 498, 126 };
            public int[] scheme3_urlBarClickPoint = new int[] { 50, 50 };
            public int[] scheme3_seHomePageInSearchBarPoint = new int[] { 205, 50 };
            public int[] scheme3_seHomePageOutSearchBarPoint = new int[] { 424, 50 };
            public int[] scheme3_seResultPageInSearchBarPoint = new int[] { 50, 126 };
            public int[] scheme3_seResultPageOutSearchBarPoint = new int[] { 269, 126 };
            public int[] scheme3_seResultPageLogoPoint = new int[] { 498, 126 };
        }

        //Public variables
        public LoadedData loadedData = null;

        //Core methods

        public Preferences()
        {
            //Check if save file exists
            bool saveExists = File.Exists((Directory.GetCurrentDirectory() + @"/Content/prefs.json"));

            //If have a save file, load it
            if (saveExists == true)
                Load();
            //If a save file don't exists, create it
            if (saveExists == false)
                Save();
        }

        private void Load()
        {
            //Load the data
            string loadedDataString = File.ReadAllText((Directory.GetCurrentDirectory() + @"/Content/prefs.json"));

            //Convert it to a loaded data object
            loadedData = JsonConvert.DeserializeObject<LoadedData>(loadedDataString);
        }

        //Public methods

        public void Save()
        {
            //If the loaded data is null, create one
            if (loadedData == null)
                loadedData = new LoadedData();

            //Save the data
            File.WriteAllText((Directory.GetCurrentDirectory() + @"/Content/prefs.json"), JsonConvert.SerializeObject(loadedData));

            //Load the data to update loaded data
            Load();
        }
    }

    /*
     * Auxiliar classes
     * 
     * Classes that are objects that will be used, only to organize data inside 
     * "LoadedData" object in the saves.
    */

    public class SaveInfo
    {
        public string key = "";
        public string value = "";
    }
}
