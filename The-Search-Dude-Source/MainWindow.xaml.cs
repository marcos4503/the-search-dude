using CoroutinesDotNet;
using CoroutinesForWpf;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using The_Search_Dude.Scripts;

namespace The_Search_Dude
{
    /*
     * This class holds and handles the central logic of The Search Dude program.
    */

    public partial class MainWindow : Window
    {
        //Enums of script
        public enum ProgramMode
        {
            Idle,
            Searching
        }
        public enum SearchStepType
        {
            Unknown,
            Search,
            Pause
        }

        //Public classes
        public class SearchStep
        {
            public SearchStepType stepType = SearchStepType.Unknown;
            public int delayInSeconds;
        }

        //Cache variables
        private ProgramMode currentProgramMode = ProgramMode.Idle;
        private WindowUsageHelp usageHelpWindow = null;
        private WindowPositionPicker controlUrlBarPositionPickerWindow = null;
        private WindowPositionPicker controlSearchEngineHomePageInSearchBarPositionPickerWindow = null;
        private WindowPositionPicker controlSearchEngineHomePageOutSearchBarPositionPickerWindow = null;
        private WindowPositionPicker controlSearchEngineResultPageInSearchBarPositionPickerWindow = null;
        private WindowPositionPicker controlSearchEngineResultPageOutSearchBarPositionPickerWindow = null;
        private WindowPositionPicker controlSearchEngineResultPageLogoPositionPickerWindow = null;
        private WindowClickRender clickRenderWindow = null;
        private WindowKeyRender keyRenderWindow = null;
        private WindowSearchStatus searchStatusWindow = null;
        private WindowNoticeDisplay noticeDisplayWindow = null;
        private IDisposable renderMouseClickCoroutine = null;
        private IDisposable renderKeyPressCoroutine = null;
        private IDisposable onMouseMoveStopSearchRoutineCoroutine = null;
        private IDisposable searchTaskRoutineTimeDisplayCoroutine = null;
        private MediaPlayer mouseClickSound = null;
        private MediaPlayer keyPressSound = null;
        private MediaPlayer taskDoneSound = null;
        private KeyboardKeys_Watcher keyboardKeysWatcher = null;
        private KeyboardHotkey_Interceptor keybordHotkeyInterceptor_stopSerchTaskHotkey = null;
        private bool isWaitingF10PressToContinue = false;
        private bool isWaitingCtrlF10PressToStop = false;
        private bool isPendingCancelOfSearchTask = false;
        private POINT lastKnowedMousePosition = new POINT(0, 0);
        List<string> searchThermsToBeUsed = new List<string>();

        //Public variables
        public Preferences programPrefs = null;

        //Core methods

        public MainWindow()
        {
            //Initialize the Window
            InitializeComponent();

            //Load the program preferences and get it
            programPrefs = new Preferences();

            //Inform the save informations and save it
            SaveInfo saveInfo = new SaveInfo();
            saveInfo.key = "saveVersion";
            saveInfo.value = "1.0.0";
            programPrefs.loadedData.saveInfo = new SaveInfo[] { saveInfo };
            programPrefs.Save();

            //Prepare the Save Automatic Search Preferences button
            saveAutoSearchPrefsBtn.Click += (s, e) => { SaveAutomaticSearchPreferencesFromUI(); };
            //Prepare the Save Control Input Parameters button
            saveCtrlInputParamsBtn.Click += (s, e) => { SaveControlInputParametersFromUI(schemeSelectorCbx.SelectedIndex); };
            //Prepare the search button
            startSearchBtn.Click += (s, e) => { StartSearchTask(); };
            moreSearchOptsBtn.Click += (s, e) =>
            {
                //If don't have a context menu yet, create it
                if (moreSearchOptsBtn.ContextMenu == null)
                {
                    //Prepare the context menu
                    moreSearchOptsBtn.ContextMenu = new ContextMenu();

                    //Add the option for start Search Using Scheme 1 Then Scheme 2
                    MenuItem searchOption1 = new MenuItem();
                    searchOption1.Header = "Start Search Using Scheme 1, then Scheme 2";
                    searchOption1.Click += (s, e) => { StartSearchTaskUsingScheme1ThenScheme2(); };
                    moreSearchOptsBtn.ContextMenu.Items.Add(searchOption1);
                }

                //Display the context menu
                ContextMenu contextMenu = moreSearchOptsBtn.ContextMenu;
                contextMenu.PlacementTarget = moreSearchOptsBtn;
                contextMenu.IsOpen = true;
                e.Handled = true;
            };

            //Prepare the validation of fields of Automatic Search Preferences in the UI
            PrepareAutomaticSearchPreferencesUIFieldsValidation();

            //Prepare the Position Picker Windows
            PrepareThePositionsPickersWindows();

            //Load the Automatic Search Preferences to UI
            LoadAutomaticSearchPreferencesToUI();

            //Select the last scheme selected
            if (programPrefs.loadedData.currentSelectedControlScheme == 1)
                schemeSelectorCbx.SelectedIndex = 0;
            if (programPrefs.loadedData.currentSelectedControlScheme == 2)
                schemeSelectorCbx.SelectedIndex = 1;
            if (programPrefs.loadedData.currentSelectedControlScheme == 3)
                schemeSelectorCbx.SelectedIndex = 2;

            //Prepare the auto load to UI of Control Input Parameters if scheme selector changes
            schemeSelectorCbx.SelectionChanged += (s, e) => { LoadControlInputParametersToUI(schemeSelectorCbx.SelectedIndex); };
            //Load the Control Input Parameters of the current scheme to UI
            LoadControlInputParametersToUI(schemeSelectorCbx.SelectedIndex);

            //Start the coroutine to render the Position Pickers in the UI
            IDisposable positionPickersDislayToUiCoroutine = Coroutine.Start(PositionPickersRenderToUILoopRoutine());

            //Prepare the position pickers help tooltips
            PrepareThePositionPickersHelp();

            //Prepare the Usage Help Window
            PrepareTheUsageHelpWindow();

            //Prepare the event of closing of this window
            this.Closing += (s, e) =>
            {
                //Inform that can close the help window, and close this
                usageHelpWindow.SetCloseable(true);
                usageHelpWindow.Close();

                //Release the keys watcher
                if (keyboardKeysWatcher != null)
                    keyboardKeysWatcher.Dispose();
                if (keybordHotkeyInterceptor_stopSerchTaskHotkey != null)
                    keybordHotkeyInterceptor_stopSerchTaskHotkey.Dispose();
            };

            //Prepare the Notice Display Window
            PrepareTheNoticeDisplayWindow();

            //Prepare the Search Status Window
            PrepareTheSearchStatusWindow();

            //Prepare the Click and Key render windows
            PrepareTheClickAndKeyRenderWindows();

            //Create the keys watcher object for check keys being pressed
            keyboardKeysWatcher = new KeyboardKeys_Watcher();
            keyboardKeysWatcher.OnPressKeys += (int keyCode) =>
            {
                //If is the F10 key, call the method binded to it
                if (((VirtualKeyInt)keyCode) == VirtualKeyInt.VK_F10)
                    OnPressF10ToContinueSearch();
            };

            //Register the hotkey to stop the search task
            keybordHotkeyInterceptor_stopSerchTaskHotkey = new KeyboardHotkey_Interceptor(this, 10, KeyboardHotkey_Interceptor.ModifierKeyCodes.Control, VirtualKeyInt.VK_F10);
            keybordHotkeyInterceptor_stopSerchTaskHotkey.OnPressHotkey += () => { OnPressCtrlF10ToStopSearch(); };

            //Load Search Therms from file
            LoadSearchThermsFromFile();
        }

        private void PrepareAutomaticSearchPreferencesUIFieldsValidation()
        {
            //Search Engine URL To Use
            prefSEUrlToUseTxt.RegisterOnTextChangedValidationCallback((currentInput) =>
            {
                //Prepare the value to return
                string toReturn = "";

                //Check if only have a-z, 0-9 and -
                if (Regex.IsMatch(currentInput, @"^[a-z0-9.-]+$") == false)
                    toReturn = "Invalid URL!";

                //Return the value
                return toReturn;
            });

            //Delay For First Search
            prefDelayForFirstSearchTxt.RegisterOnTextChangedValidationCallback((currentInput) =>
            {
                //Prepare the value to return
                string toReturn = "";

                //Check if is number
                int result;
                if (int.TryParse(currentInput, out result) == false)
                    toReturn = "Not Number!";

                //Return the value
                return toReturn;
            });

            //Count Of Subsequent Searches
            prefCountSubsequentSearchesTxt.RegisterOnTextChangedValidationCallback((currentInput) =>
            {
                //Prepare the value to return
                string toReturn = "";

                //Check if is number
                int result;
                if (int.TryParse(currentInput, out result) == false)
                    toReturn = "Not Number!";

                //Return the value
                return toReturn;
            });

            //Delay Of Subsequent Searches
            prefDelayBetweenSearchesMinTxt.RegisterOnTextChangedValidationCallback((currentInput) =>
            {
                //Prepare the value to return
                string toReturn = "";

                //Check if is number
                int result;
                if (int.TryParse(currentInput, out result) == false)
                    toReturn = "Not Number!";

                //Return the value
                return toReturn;
            });
            prefDelayBetweenSearchesMaxTxt.RegisterOnTextChangedValidationCallback((currentInput) =>
            {
                //Prepare the value to return
                string toReturn = "";

                //Check if is number
                int result;
                if (int.TryParse(currentInput, out result) == false)
                    toReturn = "Not Number!";

                //Return the value
                return toReturn;
            });

            //Do Pause For Each X Searches
            prefDoPauseForEachMinTxt.RegisterOnTextChangedValidationCallback((currentInput) =>
            {
                //Prepare the value to return
                string toReturn = "";

                //Check if is number
                int result;
                if (int.TryParse(currentInput, out result) == false)
                    toReturn = "Not Number!";

                //Return the value
                return toReturn;
            });
            prefDoPauseForEachMaxTxt.RegisterOnTextChangedValidationCallback((currentInput) =>
            {
                //Prepare the value to return
                string toReturn = "";

                //Check if is number
                int result;
                if (int.TryParse(currentInput, out result) == false)
                    toReturn = "Not Number!";

                //Return the value
                return toReturn;
            });

            //Duration For Needed Pauses
            prefPauseDurationMinTxt.RegisterOnTextChangedValidationCallback((currentInput) =>
            {
                //Prepare the value to return
                string toReturn = "";

                //Check if is number
                int result;
                if (int.TryParse(currentInput, out result) == false)
                    toReturn = "Not Number!";

                //Return the value
                return toReturn;
            });
            prefPauseDurationMaxTxt.RegisterOnTextChangedValidationCallback((currentInput) =>
            {
                //Prepare the value to return
                string toReturn = "";

                //Check if is number
                int result;
                if (int.TryParse(currentInput, out result) == false)
                    toReturn = "Not Number!";

                //Return the value
                return toReturn;
            });
        }

        private void PrepareThePositionsPickersWindows()
        {
            //Initialize the Control "URL Bar" Position Picker Window
            controlUrlBarPositionPickerWindow = new WindowPositionPicker(Color.FromArgb(255, 50, 149, 191), Color.FromArgb(255, 32, 112, 145), "URL Bar Click Point");
            controlUrlBarPositionPickerWindow.Show();
            controlUrlBarPositionPickerWindow.Visibility = Visibility.Visible;
            controlUrlBarPositionPickerWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            controlUrlBarPositionPickerWindow.Top = 50;
            controlUrlBarPositionPickerWindow.Left = 50;
            controlUrlBarPositionPickerWindow.ContentRendered += (s, e) => { controlUrlBarPositionPickerWindow.Owner = this; };

            //Initialize the Control "SE Home Page In Search Bar Point" Position Picker Window
            controlSearchEngineHomePageInSearchBarPositionPickerWindow = new WindowPositionPicker(Color.FromArgb(255, 125, 54, 191), Color.FromArgb(255, 81, 27, 133), "SE Home Page In Search Bar Point");
            controlSearchEngineHomePageInSearchBarPositionPickerWindow.Show();
            controlSearchEngineHomePageInSearchBarPositionPickerWindow.Visibility = Visibility.Visible;
            controlSearchEngineHomePageInSearchBarPositionPickerWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            controlSearchEngineHomePageInSearchBarPositionPickerWindow.Top = 50;
            controlSearchEngineHomePageInSearchBarPositionPickerWindow.Left = 205;
            controlSearchEngineHomePageInSearchBarPositionPickerWindow.ContentRendered += (s, e) => { controlSearchEngineHomePageInSearchBarPositionPickerWindow.Owner = this; };

            //Initialize the Control "SE Home Page Out Search Bar Point" Position Picker Window
            controlSearchEngineHomePageOutSearchBarPositionPickerWindow = new WindowPositionPicker(Color.FromArgb(255, 181, 56, 137), Color.FromArgb(255, 110, 29, 81), "SE Home Page Out Search Bar Point");
            controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Show();
            controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Visibility = Visibility.Visible;
            controlSearchEngineHomePageOutSearchBarPositionPickerWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Top = 50;
            controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Left = 424;
            controlSearchEngineHomePageOutSearchBarPositionPickerWindow.ContentRendered += (s, e) => { controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Owner = this; };

            //Initialize the Control "SE Result Page In Search Bar Point" Position Picker Window
            controlSearchEngineResultPageInSearchBarPositionPickerWindow = new WindowPositionPicker(Color.FromArgb(255, 196, 69, 75), Color.FromArgb(255, 125, 22, 28), "SE Result Page In Search Bar Point");
            controlSearchEngineResultPageInSearchBarPositionPickerWindow.Show();
            controlSearchEngineResultPageInSearchBarPositionPickerWindow.Visibility = Visibility.Visible;
            controlSearchEngineResultPageInSearchBarPositionPickerWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            controlSearchEngineResultPageInSearchBarPositionPickerWindow.Top = 126;
            controlSearchEngineResultPageInSearchBarPositionPickerWindow.Left = 50;
            controlSearchEngineResultPageInSearchBarPositionPickerWindow.ContentRendered += (s, e) => { controlSearchEngineResultPageInSearchBarPositionPickerWindow.Owner = this; };

            //Initialize the Control "SE Result Page Out Search Bar Point" Position Picker Window
            controlSearchEngineResultPageOutSearchBarPositionPickerWindow = new WindowPositionPicker(Color.FromArgb(255, 194, 150, 89), Color.FromArgb(255, 120, 89, 46), "SE Result Page Out Search Bar Point");
            controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Show();
            controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Visibility = Visibility.Visible;
            controlSearchEngineResultPageOutSearchBarPositionPickerWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Top = 126;
            controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Left = 269;
            controlSearchEngineResultPageOutSearchBarPositionPickerWindow.ContentRendered += (s, e) => { controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Owner = this; };

            //Initialize the Control "SE Result Page Logo Point" Position Picker Window
            controlSearchEngineResultPageLogoPositionPickerWindow = new WindowPositionPicker(Color.FromArgb(255, 120, 179, 80), Color.FromArgb(255, 70, 115, 40), "SE Result Page Logo Point");
            controlSearchEngineResultPageLogoPositionPickerWindow.Show();
            controlSearchEngineResultPageLogoPositionPickerWindow.Visibility = Visibility.Visible;
            controlSearchEngineResultPageLogoPositionPickerWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            controlSearchEngineResultPageLogoPositionPickerWindow.Top = 126;
            controlSearchEngineResultPageLogoPositionPickerWindow.Left = 498;
            controlSearchEngineResultPageLogoPositionPickerWindow.ContentRendered += (s, e) => { controlSearchEngineResultPageLogoPositionPickerWindow.Owner = this; };
        }
    
        private IEnumerator PositionPickersRenderToUILoopRoutine()
        {
            //Prepare the delay
            WaitForSeconds loopDelay = new WaitForSeconds(0.25f);

            //Start the loop
            while (true)
            {
                //Render the Position Pickers positions in the UI
                ctrlUrlBarPointX.Text = controlUrlBarPositionPickerWindow.Left.ToString();
                ctrlUrlBarPointY.Text = controlUrlBarPositionPickerWindow.Top.ToString();
                ctrlSEHomeInBarPointX.Text = controlSearchEngineHomePageInSearchBarPositionPickerWindow.Left.ToString();
                ctrlSEHomeInBarPointY.Text = controlSearchEngineHomePageInSearchBarPositionPickerWindow.Top.ToString();
                ctrlSEHomeOutBarPointX.Text = controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Left.ToString();
                ctrlSEHomeOutBarPointY.Text = controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Top.ToString();
                ctrlSEResultInBarPointX.Text = controlSearchEngineResultPageInSearchBarPositionPickerWindow.Left.ToString();
                ctrlSEResultInBarPointY.Text = controlSearchEngineResultPageInSearchBarPositionPickerWindow.Top.ToString();
                ctrlSEResultOutBarPointX.Text = controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Left.ToString();
                ctrlSEResultOutBarPointY.Text = controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Top.ToString();
                ctrlSEResultLogoPointX.Text = controlSearchEngineResultPageLogoPositionPickerWindow.Left.ToString();
                ctrlSEResultLogoPointY.Text = controlSearchEngineResultPageLogoPositionPickerWindow.Top.ToString();

                //Wait the delay
                yield return loopDelay;
            }
        }

        private void PrepareThePositionPickersHelp()
        {
            //Prepare the position pickers help tooltips
            ctrlUrlBarHelp.ToolTip         = "The same position you would click to bring your browser's URL Bar into\n" +
                                             "focus, to start typing a URL.";
            ctrlSEHomeInBarHelp.ToolTip    = "On the Home Page of the Search Engine, it is the same position where you\n" +
                                             "would click, to bring the Page Search Bar into focus to start typing.";
            ctrlSEHomeOutBarHelp.ToolTip   = "On the Home Page of the Search Engine, this is the same position you would\n" +
                                             "click to take the Page Search Bar OUT of focus. It needs to be an empty\n" +
                                             "place on the Page, which when clicked, will do nothing.";
            ctrlSEResultInBarHelp.ToolTip  = "On the Search Results Page (after doing a search using your Search Engine's\n" +
                                             "Home Page), it is the same position you would click to bring the Search Bar\n" +
                                             "into focus, so you can start typing.";
            ctrlSEResultOutBarHelp.ToolTip = "On the Search Results Page (after doing a search using your Search Engine's\n" +
                                             "Home Page), it is the same position, where you would click to take the Search\n" +
                                             "Bar OUT of focus. It needs to be an empty place, which will do nothing\n" +
                                             "if clicked.";
            ctrlSEResultLogoHelp.ToolTip   = "On the Search Results Page (after performing a search using your Search\n" +
                                             "Engine's Home Page), it is the same position, where you would click on the\n" +
                                             "Search Engine's logo, to return to its Home Page.";
        }

        private void PrepareTheUsageHelpWindow()
        {
            //Initialize the "Usage Help" Window
            usageHelpWindow = new WindowUsageHelp();
            usageHelpWindow.Show();
            usageHelpWindow.Visibility = Visibility.Visible;
            usageHelpWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            usageHelpWindow.ContentRendered += (s, e) => { 
                usageHelpWindow.Owner = this;
                usageHelpWindow.Top = this.Top;
                usageHelpWindow.Left = (this.Left + 360 + 16);
            };

            //Inform that can't be closed
            usageHelpWindow.SetCloseable(false);

            //Set this on focus
            this.Focus();
        }

        private void StartSearchTask()
        {
            //Try to save the preferences
            bool wasSaved = SaveAutomaticSearchPreferencesFromUI();

            //If not saved, stop here
            if (wasSaved == false)
            {
                MessageBox.Show("Fix the invalid fields before start the Searching!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //Change program mode to "Searching"
            if (GetCurrentProgramMode() != ProgramMode.Searching)
                SetProgramMode(ProgramMode.Searching);

            //Show the starting warning
            searchStatusWindow.SetProgressValue(-1, -1);
            searchStatusWindow.SetSearchCountText(0, programPrefs.loadedData.countOfSubsequentSearches);
            searchStatusWindow.SetRemainingTimeText(-1, -1);
            searchStatusWindow.SetCustomStatus("Open your Browser, maximize it and put it in Focus. Then, press F10 to start.");

            //Inform that is waiting F10 press
            isWaitingF10PressToContinue = true;
        }

        private void OnPressF10ToContinueSearch()
        {
            //If is not waiting for F10, cancel here
            if (isWaitingF10PressToContinue == false)
                return;

            //Start the search task routine
            IDisposable searchTaskRoutine = Coroutine.Start(SearchTaskRoutine());

            //Inform that is not waiting for F10 anymore
            isWaitingF10PressToContinue = false;
        }

        private IEnumerator SearchTaskRoutine()
        {
            //Change the progress bar of status display
            searchStatusWindow.SetProgressValue(0, 100);

            //Update the status text of the status display
            searchStatusWindow.SetCustomStatus("Press CTRL + F10 to Stop Search");

            //Inform that is waiting for CTRL + F10 to stop
            isWaitingCtrlF10PressToStop = true;
            //If the mouse movement detection is enabled, start the detection loop to stop
            if (programPrefs.loadedData.stopSearchingOnMouseMove == true)
            {
                lastKnowedMousePosition = GetCursorGlobalPosition();
                onMouseMoveStopSearchRoutineCoroutine = Coroutine.Start(OnMouseMoveStopSearchRoutineLoop());
            }

            //Pre-calculate the search routine, when will do pauses, searches, etc
            List<SearchStep> searchStepsForecast = new List<SearchStep>();
            int searchesRemaning = programPrefs.loadedData.countOfSubsequentSearches;
            while (true)
            {
                //Calculate when will have a pause
                int doPauseAfterXSerches = (new Random()).Next(programPrefs.loadedData.doPauseForEachXSearches[0], (programPrefs.loadedData.doPauseForEachXSearches[1] + 1));

                //Create all search steps before pause
                for (int i = 0; i < doPauseAfterXSerches; i++)
                {
                    //If not have more searches, stop the loop here
                    if (searchesRemaning == 0)
                        break;

                    //Create this search step
                    SearchStep searchStep = new SearchStep();
                    searchStep.stepType = SearchStepType.Search;
                    searchStep.delayInSeconds = (new Random()).Next(programPrefs.loadedData.delayOfSubsequentSearches[0], (programPrefs.loadedData.delayOfSubsequentSearches[1] + 1));
                    searchStepsForecast.Add(searchStep);

                    //Decrease the searches remaning
                    searchesRemaning -= 1;
                }

                //If not have more searches, stop the loop here
                if (searchesRemaning == 0)
                    break;

                //Create the pause step
                SearchStep pauseStep = new SearchStep();
                pauseStep.stepType = SearchStepType.Pause;
                pauseStep.delayInSeconds = (new Random()).Next(programPrefs.loadedData.durationForNeededPauses[0], (programPrefs.loadedData.durationForNeededPauses[1] + 1));
                searchStepsForecast.Add(pauseStep);
            }

            //Start the routine to show the estimated remaning time
            int totalEstimatedTime = 0;
            foreach (SearchStep ss in searchStepsForecast)
            {
                if (ss.stepType == SearchStepType.Search)
                    totalEstimatedTime += (ss.delayInSeconds + 7);
                if (ss.stepType == SearchStepType.Pause)
                    totalEstimatedTime += (ss.delayInSeconds + 2);
            }
            totalEstimatedTime += 35;
            searchTaskRoutineTimeDisplayCoroutine = Coroutine.Start(SearchTaskRoutineTimeDisplayRoutine(totalEstimatedTime));

            //Prepare the cursor click positions
            POINT urlBarClickPoint = new POINT();
            if (schemeSelectorCbx.SelectedIndex == 0) { urlBarClickPoint = new POINT(programPrefs.loadedData.scheme1_urlBarClickPoint[0], programPrefs.loadedData.scheme1_urlBarClickPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 1) { urlBarClickPoint = new POINT(programPrefs.loadedData.scheme2_urlBarClickPoint[0], programPrefs.loadedData.scheme2_urlBarClickPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 2) { urlBarClickPoint = new POINT(programPrefs.loadedData.scheme3_urlBarClickPoint[0], programPrefs.loadedData.scheme3_urlBarClickPoint[1]); }
            POINT seHomePageInSearchBarPoint = new POINT();
            if (schemeSelectorCbx.SelectedIndex == 0) { seHomePageInSearchBarPoint = new POINT(programPrefs.loadedData.scheme1_seHomePageInSearchBarPoint[0], programPrefs.loadedData.scheme1_seHomePageInSearchBarPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 1) { seHomePageInSearchBarPoint = new POINT(programPrefs.loadedData.scheme2_seHomePageInSearchBarPoint[0], programPrefs.loadedData.scheme2_seHomePageInSearchBarPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 2) { seHomePageInSearchBarPoint = new POINT(programPrefs.loadedData.scheme3_seHomePageInSearchBarPoint[0], programPrefs.loadedData.scheme3_seHomePageInSearchBarPoint[1]); }
            POINT seHomePageOutSearchBarPoint = new POINT();
            if (schemeSelectorCbx.SelectedIndex == 0) { seHomePageOutSearchBarPoint = new POINT(programPrefs.loadedData.scheme1_seHomePageOutSearchBarPoint[0], programPrefs.loadedData.scheme1_seHomePageOutSearchBarPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 1) { seHomePageOutSearchBarPoint = new POINT(programPrefs.loadedData.scheme2_seHomePageOutSearchBarPoint[0], programPrefs.loadedData.scheme2_seHomePageOutSearchBarPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 2) { seHomePageOutSearchBarPoint = new POINT(programPrefs.loadedData.scheme3_seHomePageOutSearchBarPoint[0], programPrefs.loadedData.scheme3_seHomePageOutSearchBarPoint[1]); }
            POINT seResultPageInSearchBarPoint = new POINT();
            if (schemeSelectorCbx.SelectedIndex == 0) { seResultPageInSearchBarPoint = new POINT(programPrefs.loadedData.scheme1_seResultPageInSearchBarPoint[0], programPrefs.loadedData.scheme1_seResultPageInSearchBarPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 1) { seResultPageInSearchBarPoint = new POINT(programPrefs.loadedData.scheme2_seResultPageInSearchBarPoint[0], programPrefs.loadedData.scheme2_seResultPageInSearchBarPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 2) { seResultPageInSearchBarPoint = new POINT(programPrefs.loadedData.scheme3_seResultPageInSearchBarPoint[0], programPrefs.loadedData.scheme3_seResultPageInSearchBarPoint[1]); }
            POINT seResultPageOutSearchBarPoint = new POINT();
            if (schemeSelectorCbx.SelectedIndex == 0) { seResultPageOutSearchBarPoint = new POINT(programPrefs.loadedData.scheme1_seResultPageOutSearchBarPoint[0], programPrefs.loadedData.scheme1_seResultPageOutSearchBarPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 1) { seResultPageOutSearchBarPoint = new POINT(programPrefs.loadedData.scheme2_seResultPageOutSearchBarPoint[0], programPrefs.loadedData.scheme2_seResultPageOutSearchBarPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 2) { seResultPageOutSearchBarPoint = new POINT(programPrefs.loadedData.scheme3_seResultPageOutSearchBarPoint[0], programPrefs.loadedData.scheme3_seResultPageOutSearchBarPoint[1]); }
            POINT seResultPageLogoPoint = new POINT();
            if (schemeSelectorCbx.SelectedIndex == 0) { seResultPageLogoPoint = new POINT(programPrefs.loadedData.scheme1_seResultPageLogoPoint[0], programPrefs.loadedData.scheme1_seResultPageLogoPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 1) { seResultPageLogoPoint = new POINT(programPrefs.loadedData.scheme2_seResultPageLogoPoint[0], programPrefs.loadedData.scheme2_seResultPageLogoPoint[1]); }
            if (schemeSelectorCbx.SelectedIndex == 2) { seResultPageLogoPoint = new POINT(programPrefs.loadedData.scheme3_seResultPageLogoPoint[0], programPrefs.loadedData.scheme3_seResultPageLogoPoint[1]); }

            //Wait one second
            yield return new WaitForSeconds(1.0f);

            //If is pending the cancelation of this search task, stop here
            if (isPendingCancelOfSearchTask == true)
                goto SearchStopPoint;

            //Prepare the delay between each key press
            WaitForSeconds delayKeyPress = new WaitForSeconds(0.2f);

            //Move the cursor to the Browser URL bar
            SetCursorGlobalPosition(urlBarClickPoint.X, urlBarClickPoint.Y);
            lastKnowedMousePosition = GetCursorGlobalPosition();
            //Do the click
            DoLeftMouseClickDownAndUp();
            RenderMouseClickAt(urlBarClickPoint.X, urlBarClickPoint.Y);
            //Wait one second
            yield return new WaitForSeconds(1.0f);
            //Clear the possible content
            DoKeyHotkeyKeyboardPress(VirtualKeyHex.VK_CONTROL, VirtualKeyHex.VK_A);
            DoKeyDownAndUpSingleKeyboardPress(VirtualKeyHex.VK_BACK);
            //Simulate typing a random search therm
            string urlToAccess = programPrefs.loadedData.searchEngineUrlToUse;
            for (int i = 0; i < urlToAccess.Length; i++){
                //Wait delay for key press
                yield return delayKeyPress;
                //If is pending the cancelation of this search task, stop here
                if (isPendingCancelOfSearchTask == true)
                    goto SearchStopPoint;
                //Do the key press
                RenderKeyPress(urlToAccess[i].ToString().ToUpper());
                DoKeyDownAndUpSingleKeyboardPress(ConvertCharToVirtualKeyHex(urlToAccess[i]));
            }
            //Wait one second
            yield return new WaitForSeconds(0.5f);
            //Simulate enter to run the search
            DoKeyDownAndUpSingleKeyboardPress(VirtualKeyHex.VK_RETURN);

            //Prepare the micro delay, for delays
            WaitForSeconds microDelay = new WaitForSeconds(0.1f);

            //Wait the delay
            int firstTimeToWait = programPrefs.loadedData.delayToFirstSearch;
            int firstTimeToWaitTotalMicroDelays = (programPrefs.loadedData.delayToFirstSearch * 10);
            int firstTimeToWaitCurrentMicroDelays = 0;
            while (firstTimeToWait > 0)
            {
                int microDelaysRemaining = 10;
                while (microDelaysRemaining > 0)
                {
                    yield return microDelay;
                    firstTimeToWaitCurrentMicroDelays += 1;
                    searchStatusWindow.SetProgressValue(firstTimeToWaitCurrentMicroDelays, firstTimeToWaitTotalMicroDelays);
                    microDelaysRemaining -= 1;
                }
                if (isPendingCancelOfSearchTask == true)
                    goto SearchStopPoint;
                firstTimeToWait -= 1;
            }
            searchStatusWindow.SetProgressValue(0, 100);

            //Move the cursor to the Search Engine Home Page Out Search Bar
            SetCursorGlobalPosition(seHomePageOutSearchBarPoint.X, seHomePageOutSearchBarPoint.Y);
            lastKnowedMousePosition = GetCursorGlobalPosition();
            //Do the click
            DoLeftMouseClickDownAndUp();
            RenderMouseClickAt(seHomePageOutSearchBarPoint.X, seHomePageOutSearchBarPoint.Y);
            //Wait one second
            yield return new WaitForSeconds(1.0f);
            //Move the cursor to the Search Engine Home Page In Search Bar
            SetCursorGlobalPosition(seHomePageInSearchBarPoint.X, seHomePageInSearchBarPoint.Y);
            lastKnowedMousePosition = GetCursorGlobalPosition();
            //Do the click
            DoLeftMouseClickDownAndUp();
            RenderMouseClickAt(seHomePageInSearchBarPoint.X, seHomePageInSearchBarPoint.Y);
            //Wait one second
            yield return new WaitForSeconds(1.0f);
            //Clear the possible content
            DoKeyHotkeyKeyboardPress(VirtualKeyHex.VK_CONTROL, VirtualKeyHex.VK_A);
            yield return new WaitForSeconds(0.25f);
            DoKeyDownAndUpSingleKeyboardPress(VirtualKeyHex.VK_BACK);
            //Simulate typing a random search therm
            string firstSearchTherm = GetUniqueSearchTherm();
            for (int i = 0; i < firstSearchTherm.Length; i++)
            {
                //Wait delay for key press
                yield return delayKeyPress;
                //If is pending the cancelation of this search task, stop here
                if (isPendingCancelOfSearchTask == true)
                    goto SearchStopPoint;
                //Do the key press
                RenderKeyPress(firstSearchTherm[i].ToString().ToUpper());
                DoKeyDownAndUpSingleKeyboardPress(ConvertCharToVirtualKeyHex(firstSearchTherm[i]));
            }
            //Wait one second
            yield return new WaitForSeconds(0.5f);
            //Simulate enter to run the search
            DoKeyDownAndUpSingleKeyboardPress(VirtualKeyHex.VK_RETURN);
            //Wait one second
            yield return new WaitForSeconds(1.0f);
            //Move the cursor to the Search Engine Result Page Out Search Bar
            SetCursorGlobalPosition(seResultPageOutSearchBarPoint.X, seResultPageOutSearchBarPoint.Y);
            lastKnowedMousePosition = GetCursorGlobalPosition();
            //Do the click
            DoLeftMouseClickDownAndUp();
            RenderMouseClickAt(seResultPageOutSearchBarPoint.X, seResultPageOutSearchBarPoint.Y);

            //Wait the delay
            int secondTimeToWait = programPrefs.loadedData.delayToFirstSearch;
            int secondTimeToWaitTotalMicroDelays = (programPrefs.loadedData.delayToFirstSearch * 10);
            int secondTimeToWaitCurrentMicroDelays = 0;
            while (secondTimeToWait > 0)
            {
                int microDelaysRemaining = 10;
                while (microDelaysRemaining > 0)
                {
                    yield return microDelay;
                    secondTimeToWaitCurrentMicroDelays += 1;
                    searchStatusWindow.SetProgressValue(secondTimeToWaitCurrentMicroDelays, secondTimeToWaitTotalMicroDelays);
                    microDelaysRemaining -= 1;
                }
                if (isPendingCancelOfSearchTask == true)
                    goto SearchStopPoint;
                secondTimeToWait -= 1;
            }
            searchStatusWindow.SetProgressValue(0, 100);

            //Start the searching loop, for the subsequent searches
            int subsequentSearchesCount = 0;
            while (true)
            {
                //If don't have more search steps, break this loop
                if (searchStepsForecast.Count() == 0)
                    break;

                //If is pending the cancelation of this search task, stop here
                if (isPendingCancelOfSearchTask == true)
                    goto SearchStopPoint;

                //Pick the next search step in steps forecast list
                SearchStep nextSearchStep = searchStepsForecast[0];
                searchStepsForecast.RemoveAt(0);

                //If this search step, is a search...
                if (nextSearchStep.stepType == SearchStepType.Search)
                {
                    //Move the cursor to the Search Engine Result Page Out Search Bar
                    SetCursorGlobalPosition(seResultPageOutSearchBarPoint.X, seResultPageOutSearchBarPoint.Y);
                    lastKnowedMousePosition = GetCursorGlobalPosition();
                    //Do the click
                    DoLeftMouseClickDownAndUp();
                    RenderMouseClickAt(seResultPageOutSearchBarPoint.X, seResultPageOutSearchBarPoint.Y);
                    //Wait one second
                    yield return new WaitForSeconds(1.5f);
                    //Move the cursor to the Search Engine Result Page In Search Bar
                    SetCursorGlobalPosition(seResultPageInSearchBarPoint.X, seResultPageInSearchBarPoint.Y);
                    lastKnowedMousePosition = GetCursorGlobalPosition();
                    //Do the click
                    DoLeftMouseClickDownAndUp();
                    RenderMouseClickAt(seResultPageInSearchBarPoint.X, seResultPageInSearchBarPoint.Y);
                    //Wait one second
                    yield return new WaitForSeconds(1.0f);
                    //Clear the possible content
                    DoKeyHotkeyKeyboardPress(VirtualKeyHex.VK_CONTROL, VirtualKeyHex.VK_A);
                    yield return new WaitForSeconds(0.25f);
                    DoKeyDownAndUpSingleKeyboardPress(VirtualKeyHex.VK_BACK);
                    //Simulate typing a random search therm
                    string subsequentSearchTherm = GetUniqueSearchTherm();
                    for (int i = 0; i < subsequentSearchTherm.Length; i++)
                    {
                        //Wait delay for key press
                        yield return delayKeyPress;
                        //If is pending the cancelation of this search task, stop here
                        if (isPendingCancelOfSearchTask == true)
                            goto SearchStopPoint;
                        //Do the key press
                        RenderKeyPress(subsequentSearchTherm[i].ToString().ToUpper());
                        DoKeyDownAndUpSingleKeyboardPress(ConvertCharToVirtualKeyHex(subsequentSearchTherm[i]));
                    }
                    //Wait one second
                    yield return new WaitForSeconds(0.5f);
                    //Simulate enter to run the search
                    DoKeyDownAndUpSingleKeyboardPress(VirtualKeyHex.VK_RETURN);
                    //Wait one second
                    yield return new WaitForSeconds(1.5f);
                    //Move the cursor to the Search Engine Result Page Out Search Bar
                    SetCursorGlobalPosition(seResultPageOutSearchBarPoint.X, seResultPageOutSearchBarPoint.Y);
                    lastKnowedMousePosition = GetCursorGlobalPosition();
                    //Do the click
                    DoLeftMouseClickDownAndUp();
                    RenderMouseClickAt(seResultPageOutSearchBarPoint.X, seResultPageOutSearchBarPoint.Y);

                    //Increase the subsequent search count
                    subsequentSearchesCount += 1;
                }

                //If this search step, is a pause...
                if (nextSearchStep.stepType == SearchStepType.Pause)
                {
                    //Update the status text of the status display
                    searchStatusWindow.SetCustomStatus("In Pause Now --:--");
                }

                //Update the searches count in status
                searchStatusWindow.SetSearchCountText(subsequentSearchesCount, programPrefs.loadedData.countOfSubsequentSearches);

                //Wait the delay
                int subsequentTimeToWait = nextSearchStep.delayInSeconds;
                int subsequentTimeToWaitTotalMicroDelays = (nextSearchStep.delayInSeconds * 10);
                int subsequentTimeToWaitCurrentMicroDelays = 0;
                while (subsequentTimeToWait > 0)
                {
                    int microDelaysRemaining = 10;
                    while (microDelaysRemaining > 0)
                    {
                        yield return microDelay;
                        subsequentTimeToWaitCurrentMicroDelays += 1;
                        searchStatusWindow.SetProgressValue(subsequentTimeToWaitCurrentMicroDelays, subsequentTimeToWaitTotalMicroDelays);
                        microDelaysRemaining -= 1;
                    }
                    if (isPendingCancelOfSearchTask == true)
                        goto SearchStopPoint;
                    subsequentTimeToWait -= 1;

                    //If is a pause, render the time
                    if (nextSearchStep.stepType == SearchStepType.Pause)
                    {
                        int remaningSecs = subsequentTimeToWait;
                        int remainMinutes = 0;
                        while (remaningSecs >= 60)
                        {
                            remainMinutes += 1;
                            remaningSecs -= 60;
                        }
                        string remainMinutesStr = "";
                        string remainSecsStr = "";
                        if (remainMinutes < 10)
                            remainMinutesStr = ("0" + remainMinutes);
                        if (remainMinutes >= 10)
                            remainMinutesStr = remainMinutes.ToString();
                        if (remaningSecs < 10)
                            remainSecsStr = ("0" + remaningSecs);
                        if (remaningSecs >= 10)
                            remainSecsStr = remaningSecs.ToString();
                        searchStatusWindow.SetCustomStatus("In Pause Now " + remainMinutesStr + ":" + remainSecsStr);
                    }
                }
                searchStatusWindow.SetProgressValue(0, 100);

                //Update the status text of the status display
                searchStatusWindow.SetCustomStatus("Press CTRL + F10 to Stop Search");

                //Wait the delay
                yield return new WaitForSeconds(1.0f);
            }

            //Play the finish sound
            if (taskDoneSound.Position != TimeSpan.Zero)
                taskDoneSound.Position = TimeSpan.Zero;
            taskDoneSound.Volume = (programPrefs.loadedData.simulatedInputsVolume / 100.0f);
            taskDoneSound.Play();

        //Create the stop checkpoint
        SearchStopPoint:

            //Stop the timer display
            if (searchTaskRoutineTimeDisplayCoroutine != null)
            {
                searchTaskRoutineTimeDisplayCoroutine.Dispose();
                searchTaskRoutineTimeDisplayCoroutine = null;
            }

            //Inform that is stopped
            isWaitingCtrlF10PressToStop = false;
            isPendingCancelOfSearchTask = false;

            //If the cursor movement detection loop is running, stop it
            if (onMouseMoveStopSearchRoutineCoroutine != null)
            {
                onMouseMoveStopSearchRoutineCoroutine.Dispose();
                onMouseMoveStopSearchRoutineCoroutine = null;
            }

            //Change to stopping mode
            searchStatusWindow.SetProgressValue(0, 100);
            searchStatusWindow.SetCustomStatus("Finishing in 3 Seconds");
            yield return new WaitForSeconds(1.0f);
            searchStatusWindow.SetCustomStatus("Finishing in 2 Seconds");
            yield return new WaitForSeconds(1.0f);
            searchStatusWindow.SetCustomStatus("Finishing in 1 Second");
            yield return new WaitForSeconds(1.0f);
            searchStatusWindow.SetCustomStatus("Restoring UI");
            yield return new WaitForSeconds(1.0f);

            //Change back to idle mode
            if (GetCurrentProgramMode() != ProgramMode.Idle)
                SetProgramMode(ProgramMode.Idle);
        }

        private IEnumerator SearchTaskRoutineTimeDisplayRoutine(int totalSecondsEstimated)
        {
            //Prepare the interval time
            WaitForSeconds intervalTime = new WaitForSeconds(1.0f);

            //Store the remaining time
            int remainingTime = totalSecondsEstimated;

            //Start the display loop
            while (true)
            {
                //If the remaning time is less or equal to zero, set to zero
                if (remainingTime <= 0)
                    remainingTime = 0;

                //Render the remaining time
                int remaningSecs = remainingTime;
                int remainMinutes = 0;
                while (remaningSecs >= 60)
                {
                    remainMinutes += 1;
                    remaningSecs -= 60;
                }
                searchStatusWindow.SetRemainingTimeText(remainMinutes, remaningSecs);

                //Wait the interval
                yield return intervalTime;

                //Decrese the remaining time by 1 second
                remainingTime -= 1;
            }
        }

        private void OnPressCtrlF10ToStopSearch()
        {
            //If is not waiting for Ctrl + F10, cancel here
            if (isWaitingCtrlF10PressToStop == false)
                return;

            //Inform that the cancel of the searching task is pending
            isPendingCancelOfSearchTask = true;

            //Inform that is not waiting for Ctrl + F10 anymore
            isWaitingCtrlF10PressToStop = false;
        }

        private IEnumerator OnMouseMoveStopSearchRoutineLoop()
        {
            //Prepare the delay
            WaitForSeconds loopDelay = new WaitForSeconds(0.1f);

            //Start the detection loop
            while (true)
            {
                //Get the current cursor position
                POINT currentCursorPos = GetCursorGlobalPosition();

                //If the current position is different from the last knowed, send signal to stop search task
                if (lastKnowedMousePosition.X != currentCursorPos.X && lastKnowedMousePosition.Y != currentCursorPos.Y)
                {
                    isPendingCancelOfSearchTask = true;
                    isWaitingCtrlF10PressToStop = false;
                } 

                //Wait the delay
                yield return loopDelay;
            }
        }

        private void StartSearchTaskUsingScheme1ThenScheme2()
        {
            //Start the routine that will handle the automatic search using Scheme 1 and Scheme 2
            IDisposable automaticSearchHandleRoutine = Coroutine.Start(SearchTaskRoutineScheme1Scheme2AutomaticHandlerRoutine());
        }

        private IEnumerator SearchTaskRoutineScheme1Scheme2AutomaticHandlerRoutine()
        {
            //Enable the interaction blocker
            inputBlock.Visibility = Visibility.Visible;

            //Wait time
            yield return new WaitForSeconds(1.0f);

            //Load the Scheme 1
            schemeSelectorCbx.SelectedIndex = 0;
            LoadControlInputParametersToUI(schemeSelectorCbx.SelectedIndex);

            //Wait time
            yield return new WaitForSeconds(3.0f);

            //Start the Search
            StartSearchTask();

            //Wait time
            yield return new WaitForSeconds(1.0f);

            //Simulate the F10 press to continue
            DoKeyDownAndUpSingleKeyboardPress(VirtualKeyHex.VK_F10);

            //Wait time
            yield return new WaitForSeconds(5.0f);

            //Start the loop to wait for finish of this search
            WaitForSeconds loop1Delay = new WaitForSeconds(1.0f);
            while (true)
            {
                //If exited from the searching mode, stop this loop
                if (GetCurrentProgramMode() != ProgramMode.Searching)
                    break;
                //Wait the loop
                yield return loop1Delay;
            }

            //Wait time
            yield return new WaitForSeconds(5.0f);

            //Load the Scheme 2
            schemeSelectorCbx.SelectedIndex = 1;
            LoadControlInputParametersToUI(schemeSelectorCbx.SelectedIndex);

            //Wait time
            yield return new WaitForSeconds(3.0f);

            //Start the Search
            StartSearchTask();

            //Wait time
            yield return new WaitForSeconds(1.0f);

            //Simulate the F10 press to continue
            DoKeyDownAndUpSingleKeyboardPress(VirtualKeyHex.VK_F10);

            //Wait time
            yield return new WaitForSeconds(5.0f);

            //Start the loop to wait for finish of this search
            WaitForSeconds loop2Delay = new WaitForSeconds(1.0f);
            while (true)
            {
                //If exited from the searching mode, stop this loop
                if (GetCurrentProgramMode() != ProgramMode.Searching)
                    break;
                //Wait the loop
                yield return loop2Delay;
            }

            //Wait time
            yield return new WaitForSeconds(5.0f);

            //Load the Scheme 1 again
            schemeSelectorCbx.SelectedIndex = 0;
            LoadControlInputParametersToUI(schemeSelectorCbx.SelectedIndex);

            //Wait time
            yield return new WaitForSeconds(3.0f);

            //Disable the interaction blocker
            inputBlock.Visibility = Visibility.Collapsed;
        }

        //Private auxiliar methods

        private void SetProgramMode(ProgramMode desiredProgramMode)
        {
            //If is desired idle...
            if (desiredProgramMode == ProgramMode.Idle)
            {
                //Change the windows states
                this.Visibility = Visibility.Visible;
                usageHelpWindow.Visibility = Visibility.Visible;
                controlUrlBarPositionPickerWindow.Visibility = Visibility.Visible;
                controlSearchEngineHomePageInSearchBarPositionPickerWindow.Visibility = Visibility.Visible;
                controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Visibility = Visibility.Visible;
                controlSearchEngineResultPageInSearchBarPositionPickerWindow.Visibility = Visibility.Visible;
                controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Visibility = Visibility.Visible;
                controlSearchEngineResultPageLogoPositionPickerWindow.Visibility = Visibility.Visible;
                searchStatusWindow.Visibility = Visibility.Collapsed;
                noticeDisplayWindow.Visibility = Visibility.Collapsed;

                //Inform the new status
                currentProgramMode = desiredProgramMode;
            }

            //If is desired searching...
            if (desiredProgramMode == ProgramMode.Searching)
            {
                //Change the windows states
                this.Visibility = Visibility.Collapsed;
                usageHelpWindow.Visibility = Visibility.Collapsed;
                controlUrlBarPositionPickerWindow.Visibility = Visibility.Collapsed;
                controlSearchEngineHomePageInSearchBarPositionPickerWindow.Visibility = Visibility.Collapsed;
                controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Visibility = Visibility.Collapsed;
                controlSearchEngineResultPageInSearchBarPositionPickerWindow.Visibility = Visibility.Collapsed;
                controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Visibility = Visibility.Collapsed;
                controlSearchEngineResultPageLogoPositionPickerWindow.Visibility = Visibility.Collapsed;
                searchStatusWindow.Visibility = Visibility.Visible;
                noticeDisplayWindow.Visibility = Visibility.Visible;

                //Inform the new status
                currentProgramMode = desiredProgramMode;
            }
        }

        private ProgramMode GetCurrentProgramMode()
        {
            //Return the current program mode
            return currentProgramMode;
        }

        private void PrepareTheSearchStatusWindow()
        {
            //Initialize the "Search Status" Window
            searchStatusWindow = new WindowSearchStatus();
            searchStatusWindow.Show();
            searchStatusWindow.Visibility = Visibility.Visible;
            searchStatusWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            searchStatusWindow.Top = 0;
            searchStatusWindow.Left = 0;
            searchStatusWindow.ContentRendered += (s, e) =>
            {
                searchStatusWindow.Owner = this;
                searchStatusWindow.Left = ((SystemParameters.PrimaryScreenWidth / 2.0f) - (searchStatusWindow.Width / 2.0f));
                searchStatusWindow.Top = -4;
                searchStatusWindow.Visibility = Visibility.Collapsed;
            };
        }

        private void PrepareTheNoticeDisplayWindow()
        {
            //Initialize the "Notice Display" Window
            noticeDisplayWindow = new WindowNoticeDisplay();
            noticeDisplayWindow.Show();
            noticeDisplayWindow.Visibility = Visibility.Visible;
            noticeDisplayWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            noticeDisplayWindow.Top = 0;
            noticeDisplayWindow.Left = 0;
            noticeDisplayWindow.ContentRendered += (s, e) =>
            {
                noticeDisplayWindow.Owner = this;
                noticeDisplayWindow.Left = ((SystemParameters.PrimaryScreenWidth / 2.0f) - (noticeDisplayWindow.Width / 2.0f));
                noticeDisplayWindow.Top = ((SystemParameters.PrimaryScreenHeight / 2.0f) - (noticeDisplayWindow.Height / 2.0f) + ((SystemParameters.PrimaryScreenHeight / 2.0f) * 0.8f));
                noticeDisplayWindow.Visibility = Visibility.Collapsed;
            };
        }

        private void PrepareTheClickAndKeyRenderWindows()
        {
            //Initialize the "Click Render" Window
            clickRenderWindow = new WindowClickRender();
            clickRenderWindow.Show();
            clickRenderWindow.Visibility = Visibility.Visible;
            clickRenderWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            clickRenderWindow.Top = 0;
            clickRenderWindow.Left = 0;
            clickRenderWindow.ContentRendered += (s, e) => 
            {
                clickRenderWindow.Owner = this;
                clickRenderWindow.Top = 0;
                clickRenderWindow.Left = 0;
                clickRenderWindow.Visibility = Visibility.Collapsed;
            };

            //Initialize the "Key Render" Window
            keyRenderWindow = new WindowKeyRender();
            keyRenderWindow.Show();
            keyRenderWindow.Visibility = Visibility.Visible;
            keyRenderWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            keyRenderWindow.Top = 0;
            keyRenderWindow.Left = 0;
            keyRenderWindow.ContentRendered += (s, e) =>
            {
                keyRenderWindow.Owner = this;
                keyRenderWindow.Left = ((SystemParameters.PrimaryScreenWidth / 2.0f) - (keyRenderWindow.Width / 2.0f));
                keyRenderWindow.Top = ((SystemParameters.PrimaryScreenHeight / 2.0f) - (keyRenderWindow.Height / 2.0f));
                keyRenderWindow.Visibility = Visibility.Collapsed;
            };

            //Load the sounds
            mouseClickSound = new MediaPlayer();
            mouseClickSound.Open(new Uri(@"Resources/mouse-click.wav", UriKind.Relative));
            keyPressSound = new MediaPlayer();
            keyPressSound.Open(new Uri(@"Resources/key-press.wav", UriKind.Relative));
            taskDoneSound = new MediaPlayer();
            taskDoneSound.Open(new Uri(@"Resources/task-done.wav", UriKind.Relative));
        }

        private void LoadSearchThermsFromFile()
        {
            //Read the file
            string[] searchThermsLines = File.ReadAllLines((Directory.GetCurrentDirectory() + @"/Content/search-therms-list.txt"));

            //Add all search therms in the list to be used
            for (int i = 3; i < searchThermsLines.Length; i++)
                searchThermsToBeUsed.Add(searchThermsLines[i]);
        }

        private string GetUniqueSearchTherm()
        {
            //Prepare the search therm to return
            string searchTherm = "ABC";

            //If the list is near to empty, re-load it
            if (searchThermsToBeUsed.Count <= 5)
            {
                searchThermsToBeUsed.Clear();
                LoadSearchThermsFromFile();
            }

            //Get a random index to get the search therm
            int randomIndex = (new Random().Next(0, searchThermsToBeUsed.Count));

            //Pick the selected index and remove it from list, to not be picked again
            searchTherm = searchThermsToBeUsed[randomIndex];
            searchThermsToBeUsed.RemoveAt(randomIndex);

            //Return the search therm
            return searchTherm;
        }

        private VirtualKeyHex ConvertCharToVirtualKeyHex(char character)
        {
            //Prepare the key to return
            VirtualKeyHex toReturn = VirtualKeyHex.VK_OEM_PLUS;

            //Prepare the conversion
            if (character.ToString().ToLower() == "a") { toReturn = VirtualKeyHex.VK_A; }
            if (character.ToString().ToLower() == "b") { toReturn = VirtualKeyHex.VK_B; }
            if (character.ToString().ToLower() == "c") { toReturn = VirtualKeyHex.VK_C; }
            if (character.ToString().ToLower() == "d") { toReturn = VirtualKeyHex.VK_D; }
            if (character.ToString().ToLower() == "e") { toReturn = VirtualKeyHex.VK_E; }
            if (character.ToString().ToLower() == "f") { toReturn = VirtualKeyHex.VK_F; }
            if (character.ToString().ToLower() == "g") { toReturn = VirtualKeyHex.VK_G; }
            if (character.ToString().ToLower() == "h") { toReturn = VirtualKeyHex.VK_H; }
            if (character.ToString().ToLower() == "i") { toReturn = VirtualKeyHex.VK_I; }
            if (character.ToString().ToLower() == "j") { toReturn = VirtualKeyHex.VK_J; }
            if (character.ToString().ToLower() == "k") { toReturn = VirtualKeyHex.VK_K; }
            if (character.ToString().ToLower() == "l") { toReturn = VirtualKeyHex.VK_L; }
            if (character.ToString().ToLower() == "m") { toReturn = VirtualKeyHex.VK_M; }
            if (character.ToString().ToLower() == "n") { toReturn = VirtualKeyHex.VK_N; }
            if (character.ToString().ToLower() == "o") { toReturn = VirtualKeyHex.VK_O; }
            if (character.ToString().ToLower() == "p") { toReturn = VirtualKeyHex.VK_P; }
            if (character.ToString().ToLower() == "q") { toReturn = VirtualKeyHex.VK_Q; }
            if (character.ToString().ToLower() == "r") { toReturn = VirtualKeyHex.VK_R; }
            if (character.ToString().ToLower() == "s") { toReturn = VirtualKeyHex.VK_S; }
            if (character.ToString().ToLower() == "t") { toReturn = VirtualKeyHex.VK_T; }
            if (character.ToString().ToLower() == "u") { toReturn = VirtualKeyHex.VK_U; }
            if (character.ToString().ToLower() == "v") { toReturn = VirtualKeyHex.VK_V; }
            if (character.ToString().ToLower() == "w") { toReturn = VirtualKeyHex.VK_W; }
            if (character.ToString().ToLower() == "x") { toReturn = VirtualKeyHex.VK_X; }
            if (character.ToString().ToLower() == "y") { toReturn = VirtualKeyHex.VK_Y; }
            if (character.ToString().ToLower() == "z") { toReturn = VirtualKeyHex.VK_Z; }
            if (character.ToString().ToLower() == " ") { toReturn = VirtualKeyHex.VK_SPACE; }
            if (character.ToString().ToLower() == ".") { toReturn = VirtualKeyHex.VK_OEM_PERIOD; }
            if (character.ToString().ToLower() == "-") { toReturn = VirtualKeyHex.VK_OEM_MINUS; }
            if (character.ToString().ToLower() == "1") { toReturn = VirtualKeyHex.VK_1; }
            if (character.ToString().ToLower() == "2") { toReturn = VirtualKeyHex.VK_2; }
            if (character.ToString().ToLower() == "3") { toReturn = VirtualKeyHex.VK_3; }
            if (character.ToString().ToLower() == "4") { toReturn = VirtualKeyHex.VK_4; }
            if (character.ToString().ToLower() == "5") { toReturn = VirtualKeyHex.VK_5; }
            if (character.ToString().ToLower() == "6") { toReturn = VirtualKeyHex.VK_6; }
            if (character.ToString().ToLower() == "7") { toReturn = VirtualKeyHex.VK_7; }
            if (character.ToString().ToLower() == "8") { toReturn = VirtualKeyHex.VK_8; }
            if (character.ToString().ToLower() == "9") { toReturn = VirtualKeyHex.VK_9; }
            if (character.ToString().ToLower() == "0") { toReturn = VirtualKeyHex.VK_0; }

            //Return the key
            return toReturn;
        }

        private void RenderMouseClickAt(int screenX, int screenY)
        {
            //If the routine is already running, stop it
            if (renderMouseClickCoroutine != null)
            {
                renderMouseClickCoroutine.Dispose();
                renderMouseClickCoroutine = null;
            }

            //Start the render mouse click routine
            renderMouseClickCoroutine = Coroutine.Start(RenderMouseClickAtRoutine(screenX, screenY));
        }

        private IEnumerator RenderMouseClickAtRoutine(int screenX, int screenY)
        {
            //Render the click at the desired position
            clickRenderWindow.Left = screenX;
            clickRenderWindow.Top = screenY;
            if (clickRenderWindow.Visibility != Visibility.Visible)
                clickRenderWindow.Visibility = Visibility.Visible;

            //Play the click sound
            if (mouseClickSound.Position != TimeSpan.Zero)
                mouseClickSound.Position = TimeSpan.Zero;
            mouseClickSound.Volume = (programPrefs.loadedData.simulatedInputsVolume / 100.0f);
            mouseClickSound.Play();

            //Wait time before clear the click render
            yield return new WaitForSeconds(1.0f);

            //Hide the click render
            clickRenderWindow.Visibility = Visibility.Collapsed;

            //Clear this routine reference
            renderMouseClickCoroutine = null;
        }

        private void RenderKeyPress(string keyName)
        {
            //If the routine is already running, stop it
            if (renderKeyPressCoroutine != null)
            {
                renderKeyPressCoroutine.Dispose();
                renderKeyPressCoroutine = null;
            }

            //Start the render key press routine
            renderKeyPressCoroutine = Coroutine.Start(RenderKeyPressRoutine(keyName));
        }

        private IEnumerator RenderKeyPressRoutine(string keyName)
        {
            //Render the key
            keyRenderWindow.SetKeyText(keyName);
            if (keyRenderWindow.Visibility != Visibility.Visible)
                keyRenderWindow.Visibility = Visibility.Visible;

            //Play the key press sound
            if (keyPressSound.Position != TimeSpan.Zero)
                keyPressSound.Position = TimeSpan.Zero;
            keyPressSound.Volume = (programPrefs.loadedData.simulatedInputsVolume / 100.0f);
            keyPressSound.Play();

            //Wait time before clear the key render
            yield return new WaitForSeconds(1.0f);

            //Hide the key render
            keyRenderWindow.Visibility = Visibility.Collapsed;

            //Clear this routine reference
            renderKeyPressCoroutine = null;
        }

        private void LoadAutomaticSearchPreferencesToUI()
        {
            //Display all saved settings in UI
            prefSEUrlToUseTxt.textBox.Text = programPrefs.loadedData.searchEngineUrlToUse;
            prefDelayForFirstSearchTxt.textBox.Text = programPrefs.loadedData.delayToFirstSearch.ToString();
            prefCountSubsequentSearchesTxt.textBox.Text = programPrefs.loadedData.countOfSubsequentSearches.ToString();
            prefDelayBetweenSearchesMinTxt.textBox.Text = programPrefs.loadedData.delayOfSubsequentSearches[0].ToString();
            prefDelayBetweenSearchesMaxTxt.textBox.Text = programPrefs.loadedData.delayOfSubsequentSearches[1].ToString();
            prefDoPauseForEachMinTxt.textBox.Text = programPrefs.loadedData.doPauseForEachXSearches[0].ToString();
            prefDoPauseForEachMaxTxt.textBox.Text = programPrefs.loadedData.doPauseForEachXSearches[1].ToString();
            prefPauseDurationMinTxt.textBox.Text = programPrefs.loadedData.durationForNeededPauses[0].ToString();
            prefPauseDurationMaxTxt.textBox.Text = programPrefs.loadedData.durationForNeededPauses[1].ToString();
            prefInputsVolumeSlider.Value = programPrefs.loadedData.simulatedInputsVolume;
            prefStopSearchOnMouseMoveTg.IsChecked = programPrefs.loadedData.stopSearchingOnMouseMove;
        }

        private bool SaveAutomaticSearchPreferencesFromUI()
        {
            //Prepare to return the result
            bool toReturn = false;

            //If some field have error, cancel here
            if (prefSEUrlToUseTxt.hasError() == true || prefDelayForFirstSearchTxt.hasError() == true || prefCountSubsequentSearchesTxt.hasError() == true || prefDelayBetweenSearchesMinTxt.hasError() == true ||
                prefDelayBetweenSearchesMaxTxt.hasError() == true || prefDoPauseForEachMinTxt.hasError() == true || prefDoPauseForEachMaxTxt.hasError() == true || prefPauseDurationMinTxt.hasError() == true ||
                prefPauseDurationMaxTxt.hasError() == true)
            {
                MessageBox.Show("Fix the invalid fields before save!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return toReturn;
            }

            //Inform that the save was success
            toReturn = true;

            //Fix min and max ranges
            int prefDelayBetweenSearchesMinInt = int.Parse(prefDelayBetweenSearchesMinTxt.textBox.Text);
            int prefDelayBetweenSearchesMaxInt = int.Parse(prefDelayBetweenSearchesMaxTxt.textBox.Text);
            if (prefDelayBetweenSearchesMaxInt <= prefDelayBetweenSearchesMinInt)
                prefDelayBetweenSearchesMaxTxt.textBox.Text = (prefDelayBetweenSearchesMinInt + 1).ToString();
            int prefDoPauseForEachMinInt = int.Parse(prefDoPauseForEachMinTxt.textBox.Text);
            int prefDoPauseForEachMaxInt = int.Parse(prefDoPauseForEachMaxTxt.textBox.Text);
            if (prefDoPauseForEachMaxInt <= prefDoPauseForEachMinInt)
                prefDoPauseForEachMaxTxt.textBox.Text = (prefDoPauseForEachMinInt + 1).ToString();
            int prefPauseDurationMinInt = int.Parse(prefPauseDurationMinTxt.textBox.Text);
            int prefPauseDurationMaxInt = int.Parse(prefPauseDurationMaxTxt.textBox.Text);
            if (prefPauseDurationMaxInt <= prefPauseDurationMinInt)
                prefPauseDurationMaxTxt.textBox.Text = (prefPauseDurationMinInt + 1).ToString();

            //Save the data from UI to preferences
            programPrefs.loadedData.searchEngineUrlToUse = prefSEUrlToUseTxt.textBox.Text;
            programPrefs.loadedData.delayToFirstSearch = int.Parse(prefDelayForFirstSearchTxt.textBox.Text);
            programPrefs.loadedData.countOfSubsequentSearches = int.Parse(prefCountSubsequentSearchesTxt.textBox.Text);
            programPrefs.loadedData.delayOfSubsequentSearches[0] = int.Parse(prefDelayBetweenSearchesMinTxt.textBox.Text);
            programPrefs.loadedData.delayOfSubsequentSearches[1] = int.Parse(prefDelayBetweenSearchesMaxTxt.textBox.Text);
            programPrefs.loadedData.doPauseForEachXSearches[0] = int.Parse(prefDoPauseForEachMinTxt.textBox.Text);
            programPrefs.loadedData.doPauseForEachXSearches[1] = int.Parse(prefDoPauseForEachMaxTxt.textBox.Text);
            programPrefs.loadedData.durationForNeededPauses[0] = int.Parse(prefPauseDurationMinTxt.textBox.Text);
            programPrefs.loadedData.durationForNeededPauses[1] = int.Parse(prefPauseDurationMaxTxt.textBox.Text);
            programPrefs.loadedData.simulatedInputsVolume = ((int)prefInputsVolumeSlider.Value);
            programPrefs.loadedData.stopSearchingOnMouseMove = ((bool)prefStopSearchOnMouseMoveTg.IsChecked);

            //Save the preferences
            programPrefs.Save();

            //Return the result
            return toReturn;
        }

        private void LoadControlInputParametersToUI(int scheme)
        {
            //If is Scheme 1
            if (scheme == 0)
            {
                controlUrlBarPositionPickerWindow.Left = programPrefs.loadedData.scheme1_urlBarClickPoint[0];
                controlUrlBarPositionPickerWindow.Top = programPrefs.loadedData.scheme1_urlBarClickPoint[1];
                controlSearchEngineHomePageInSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme1_seHomePageInSearchBarPoint[0];
                controlSearchEngineHomePageInSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme1_seHomePageInSearchBarPoint[1];
                controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme1_seHomePageOutSearchBarPoint[0];
                controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme1_seHomePageOutSearchBarPoint[1];
                controlSearchEngineResultPageInSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme1_seResultPageInSearchBarPoint[0];
                controlSearchEngineResultPageInSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme1_seResultPageInSearchBarPoint[1];
                controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme1_seResultPageOutSearchBarPoint[0];
                controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme1_seResultPageOutSearchBarPoint[1];
                controlSearchEngineResultPageLogoPositionPickerWindow.Left = programPrefs.loadedData.scheme1_seResultPageLogoPoint[0];
                controlSearchEngineResultPageLogoPositionPickerWindow.Top = programPrefs.loadedData.scheme1_seResultPageLogoPoint[1];
            }

            //If is Scheme 2
            if (scheme == 1)
            {
                controlUrlBarPositionPickerWindow.Left = programPrefs.loadedData.scheme2_urlBarClickPoint[0];
                controlUrlBarPositionPickerWindow.Top = programPrefs.loadedData.scheme2_urlBarClickPoint[1];
                controlSearchEngineHomePageInSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme2_seHomePageInSearchBarPoint[0];
                controlSearchEngineHomePageInSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme2_seHomePageInSearchBarPoint[1];
                controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme2_seHomePageOutSearchBarPoint[0];
                controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme2_seHomePageOutSearchBarPoint[1];
                controlSearchEngineResultPageInSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme2_seResultPageInSearchBarPoint[0];
                controlSearchEngineResultPageInSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme2_seResultPageInSearchBarPoint[1];
                controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme2_seResultPageOutSearchBarPoint[0];
                controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme2_seResultPageOutSearchBarPoint[1];
                controlSearchEngineResultPageLogoPositionPickerWindow.Left = programPrefs.loadedData.scheme2_seResultPageLogoPoint[0];
                controlSearchEngineResultPageLogoPositionPickerWindow.Top = programPrefs.loadedData.scheme2_seResultPageLogoPoint[1];
            }

            //If is Scheme 3
            if (scheme == 2)
            {
                controlUrlBarPositionPickerWindow.Left = programPrefs.loadedData.scheme3_urlBarClickPoint[0];
                controlUrlBarPositionPickerWindow.Top = programPrefs.loadedData.scheme3_urlBarClickPoint[1];
                controlSearchEngineHomePageInSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme3_seHomePageInSearchBarPoint[0];
                controlSearchEngineHomePageInSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme3_seHomePageInSearchBarPoint[1];
                controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme3_seHomePageOutSearchBarPoint[0];
                controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme3_seHomePageOutSearchBarPoint[1];
                controlSearchEngineResultPageInSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme3_seResultPageInSearchBarPoint[0];
                controlSearchEngineResultPageInSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme3_seResultPageInSearchBarPoint[1];
                controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Left = programPrefs.loadedData.scheme3_seResultPageOutSearchBarPoint[0];
                controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Top = programPrefs.loadedData.scheme3_seResultPageOutSearchBarPoint[1];
                controlSearchEngineResultPageLogoPositionPickerWindow.Left = programPrefs.loadedData.scheme3_seResultPageLogoPoint[0];
                controlSearchEngineResultPageLogoPositionPickerWindow.Top = programPrefs.loadedData.scheme3_seResultPageLogoPoint[1];
            }

            //Save the current schema selected
            programPrefs.loadedData.currentSelectedControlScheme = (schemeSelectorCbx.SelectedIndex + 1);
            programPrefs.Save();
        }

        private void SaveControlInputParametersFromUI(int scheme)
        {
            //If is Scheme 1
            if (scheme == 0)
            {
                programPrefs.loadedData.scheme1_urlBarClickPoint[0] = (int)controlUrlBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme1_urlBarClickPoint[1] = (int)controlUrlBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme1_seHomePageInSearchBarPoint[0] = (int)controlSearchEngineHomePageInSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme1_seHomePageInSearchBarPoint[1] = (int)controlSearchEngineHomePageInSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme1_seHomePageOutSearchBarPoint[0] = (int)controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme1_seHomePageOutSearchBarPoint[1] = (int)controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme1_seResultPageInSearchBarPoint[0] = (int)controlSearchEngineResultPageInSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme1_seResultPageInSearchBarPoint[1] = (int)controlSearchEngineResultPageInSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme1_seResultPageOutSearchBarPoint[0] = (int)controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme1_seResultPageOutSearchBarPoint[1] = (int)controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme1_seResultPageLogoPoint[0] = (int)controlSearchEngineResultPageLogoPositionPickerWindow.Left;
                programPrefs.loadedData.scheme1_seResultPageLogoPoint[1] = (int)controlSearchEngineResultPageLogoPositionPickerWindow.Top;
            }

            //If is Scheme 2
            if (scheme == 1)
            {
                programPrefs.loadedData.scheme2_urlBarClickPoint[0] = (int)controlUrlBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme2_urlBarClickPoint[1] = (int)controlUrlBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme2_seHomePageInSearchBarPoint[0] = (int)controlSearchEngineHomePageInSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme2_seHomePageInSearchBarPoint[1] = (int)controlSearchEngineHomePageInSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme2_seHomePageOutSearchBarPoint[0] = (int)controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme2_seHomePageOutSearchBarPoint[1] = (int)controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme2_seResultPageInSearchBarPoint[0] = (int)controlSearchEngineResultPageInSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme2_seResultPageInSearchBarPoint[1] = (int)controlSearchEngineResultPageInSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme2_seResultPageOutSearchBarPoint[0] = (int)controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme2_seResultPageOutSearchBarPoint[1] = (int)controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme2_seResultPageLogoPoint[0] = (int)controlSearchEngineResultPageLogoPositionPickerWindow.Left;
                programPrefs.loadedData.scheme2_seResultPageLogoPoint[1] = (int)controlSearchEngineResultPageLogoPositionPickerWindow.Top;
            }

            //If is Scheme 3
            if (scheme == 2)
            {
                programPrefs.loadedData.scheme3_urlBarClickPoint[0] = (int)controlUrlBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme3_urlBarClickPoint[1] = (int)controlUrlBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme3_seHomePageInSearchBarPoint[0] = (int)controlSearchEngineHomePageInSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme3_seHomePageInSearchBarPoint[1] = (int)controlSearchEngineHomePageInSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme3_seHomePageOutSearchBarPoint[0] = (int)controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme3_seHomePageOutSearchBarPoint[1] = (int)controlSearchEngineHomePageOutSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme3_seResultPageInSearchBarPoint[0] = (int)controlSearchEngineResultPageInSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme3_seResultPageInSearchBarPoint[1] = (int)controlSearchEngineResultPageInSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme3_seResultPageOutSearchBarPoint[0] = (int)controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Left;
                programPrefs.loadedData.scheme3_seResultPageOutSearchBarPoint[1] = (int)controlSearchEngineResultPageOutSearchBarPositionPickerWindow.Top;
                programPrefs.loadedData.scheme3_seResultPageLogoPoint[0] = (int)controlSearchEngineResultPageLogoPositionPickerWindow.Left;
                programPrefs.loadedData.scheme3_seResultPageLogoPoint[1] = (int)controlSearchEngineResultPageLogoPositionPickerWindow.Top;
            }

            //Save the preferences
            programPrefs.Save();
        }

        //Private auxiliar APIs

        #region MouseUseSimulationInterface

        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        public const int MOUSEEVENTF_MIDDLEDOWN = 0x20;
        public const int MOUSEEVENTF_MIDDLEUP = 0x40;
        public const int MOUSEEVENTF_WHEEL = 0x0800;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        public const int MOUSEEVENTF_RIGHTUP = 0x10;

        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        public enum ScrollDirection
        {
            Up,
            Down
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT pPoint);
        [DllImport("User32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int X, int Y);

        public static POINT GetCursorGlobalPosition()
        {
            POINT pnt;
            GetCursorPos(out pnt);
            return pnt;
        }

        public static void SetCursorGlobalPosition(int x, int y)
        {
            SetCursorPos(x, y);
        }

        public static void DoLeftMouseClickDownAndUp()
        {
            POINT pnt;
            GetCursorPos(out pnt);

            mouse_event(MOUSEEVENTF_LEFTDOWN, pnt.X, pnt.Y, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, pnt.X, pnt.Y, 0, 0);
        }

        public static void DoLeftMouseClickDown()
        {
            POINT pnt;
            GetCursorPos(out pnt);

            mouse_event(MOUSEEVENTF_LEFTDOWN, pnt.X, pnt.Y, 0, 0);
        }

        public static void DoLeftMouseClickUp()
        {
            POINT pnt;
            GetCursorPos(out pnt);

            mouse_event(MOUSEEVENTF_LEFTUP, pnt.X, pnt.Y, 0, 0);
        }

        public static void DoMiddleMouseClickDownAndUp()
        {
            POINT pnt;
            GetCursorPos(out pnt);

            mouse_event(MOUSEEVENTF_MIDDLEDOWN, pnt.X, pnt.Y, 0, 0);
            mouse_event(MOUSEEVENTF_MIDDLEUP, pnt.X, pnt.Y, 0, 0);
        }

        public static void DoMiddleMouseClickDown()
        {
            POINT pnt;
            GetCursorPos(out pnt);

            mouse_event(MOUSEEVENTF_MIDDLEDOWN, pnt.X, pnt.Y, 0, 0);
        }

        public static void DoMiddleMouseClickUp()
        {
            POINT pnt;
            GetCursorPos(out pnt);

            mouse_event(MOUSEEVENTF_MIDDLEUP, pnt.X, pnt.Y, 0, 0);
        }

        public static void DoWheelMouseScroll(ScrollDirection direction, int stepsCount)
        {
            POINT pnt;
            GetCursorPos(out pnt);

            int WHEEL_DELTA = 120;

            if (direction == ScrollDirection.Up)
                mouse_event(MOUSEEVENTF_WHEEL, pnt.X, pnt.Y, ((WHEEL_DELTA * stepsCount) * 1), 0);

            if (direction == ScrollDirection.Down)
                mouse_event(MOUSEEVENTF_WHEEL, pnt.X, pnt.Y, ((WHEEL_DELTA * stepsCount) * -1), 0);
        }

        public static void DoRightMouseClickDownAndUp()
        {
            POINT pnt;
            GetCursorPos(out pnt);

            mouse_event(MOUSEEVENTF_RIGHTDOWN, pnt.X, pnt.Y, 0, 0);
            mouse_event(MOUSEEVENTF_RIGHTUP, pnt.X, pnt.Y, 0, 0);
        }

        public static void DoRightMouseClickDown()
        {
            POINT pnt;
            GetCursorPos(out pnt);

            mouse_event(MOUSEEVENTF_RIGHTDOWN, pnt.X, pnt.Y, 0, 0);
        }

        public static void DoRightMouseClickUp()
        {
            POINT pnt;
            GetCursorPos(out pnt);

            mouse_event(MOUSEEVENTF_RIGHTUP, pnt.X, pnt.Y, 0, 0);
        }

        #endregion

        #region KeyboardUseSimulationInterface

        public const uint KEYEVENTF_KEYDOWN = 0x0000;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        public static Dictionary<string, KeyHandleThread> simulationKeyHoldingThreads = null;

        public class KeyHandleThread
        {
            public CancellationTokenSource cancellationTokenSource = null;
            public Thread simulationHoldingThread = null;
        }

        public enum VirtualKeyHex
        {
            VK_ESCAPE = 0x1B,            //<- ESC
            VK_TAB = 0x09,
            VK_SHIFT = 0x10,
            VK_LSHIFT = 0xA0,
            VK_RSHIFT = 0xA1,
            VK_CONTROL = 0x11,
            VK_LCONTROL = 0xA2,
            VK_RCONTROL = 0xA3,
            VK_MENU = 0x12,              //<- ALT
            VK_LMENU = 0xA4,             //<- LEFT_ALT
            VK_RMENU = 0xA5,             //<- RIGHT_ALT
            VK_CAPITAL = 0x14,           //<- CAPS_LOCK
            VK_NUMLOCK = 0x90,
            VK_SCROLL = 0x91,            //<- SCROLL_LOCK
            VK_RETURN = 0x0D,            //<- ENTER
            VK_SPACE = 0x20,
            VK_PRIOR = 0x21,             //<- PAGE_UP
            VK_NEXT = 0x22,              //<- PAGE_DOWN
            VK_END = 0x23,
            VK_HOME = 0x24,
            VK_LEFT = 0x25,              //<- LEFT_ARROW
            VK_UP = 0x26,                //<- UP_ARROW
            VK_RIGHT = 0x27,             //<- RIGHT_ARROW
            VK_DOWN = 0x28,              //<- DOWN_ARROW
            VK_SNAPSHOT = 0x2C,          //<- PRINT_SCREEN
            VK_PAUSE = 0x13,
            VK_INSERT = 0x2D,
            VK_DELETE = 0x2E,
            VK_BACK = 0x08,              //<- BACKSPACE
            VK_LWIN = 0x5B,              //<- LEFT_WIN
            VK_RWIN = 0x5C,              //<- RIGHT_WIN
            VK_APPS = 0x5D,              //<- APPS_KEY/CONTEXT_MENU
            VK_NUMPAD0 = 0x60,           //<- If NumPad ON: "0" - If NumPad OFF: "INSERT"
            VK_NUMPAD1 = 0x61,           //<- If NumPad ON: "1" - If NumPad OFF: "END"
            VK_NUMPAD2 = 0x62,           //<- If NumPad ON: "2" - If NumPad OFF: "DOWN_ARROW"
            VK_NUMPAD3 = 0x63,           //<- If NumPad ON: "3" - If NumPad OFF: "PAGE_DOWN"
            VK_NUMPAD4 = 0x64,           //<- If NumPad ON: "4" - If NumPad OFF: "LEFT_ARROW"
            VK_NUMPAD5 = 0x65,           //<- If NumPad ON: "5" - If NumPad OFF: "?"
            VK_NUMPAD6 = 0x66,           //<- If NumPad ON: "6" - If NumPad OFF: "RIGHT_ARROW"
            VK_NUMPAD7 = 0x67,           //<- If NumPad ON: "7" - If NumPad OFF: "HOME"
            VK_NUMPAD8 = 0x68,           //<- If NumPad ON: "8" - If NumPad OFF: "UP_ARROW"
            VK_NUMPAD9 = 0x69,           //<- If NumPad ON: "9" - If NumPad OFF: "PAGE_UP"
            VK_MULTIPLY = 0x6A,          //<- NUMPAD_* Can be used with NumPad ON or OFF
            VK_ADD = 0x6B,               //<- NUMPAD_+ Can be used with NumPad ON or OFF
            VK_SEPARATOR = 0x6C,         //<- NUMPAD_. Can be used with NumPad ON or OFF
            VK_SUBTRACT = 0x6D,          //<- NUMPAD_- Can be used with NumPad ON or OFF
            VK_DECIMAL = 0x6E,           //<- NUMPAD_, If NumPad ON: "," - If NumPad OFF: "DELETE"
            VK_DIVIDE = 0x6F,            //<- NUMPAD_/ Can be used with NumPad ON or OFF
            VK_F1 = 0x70,
            VK_F2 = 0x71,
            VK_F3 = 0x72,
            VK_F4 = 0x73,
            VK_F5 = 0x74,
            VK_F6 = 0x75,
            VK_F7 = 0x76,
            VK_F8 = 0x77,
            VK_F9 = 0x78,
            VK_F10 = 0x79,
            VK_F11 = 0x7A,
            VK_F12 = 0x7B,
            VK_0 = 0x30,
            VK_1 = 0x31,
            VK_2 = 0x32,
            VK_3 = 0x33,
            VK_4 = 0x34,
            VK_5 = 0x35,
            VK_6 = 0x36,
            VK_7 = 0x37,
            VK_8 = 0x38,
            VK_9 = 0x39,
            VK_A = 0x41,
            VK_B = 0x42,
            VK_C = 0x43,
            VK_D = 0x44,
            VK_E = 0x45,
            VK_F = 0x46,
            VK_G = 0x47,
            VK_H = 0x48,
            VK_I = 0x49,
            VK_J = 0x4A,
            VK_K = 0x4B,
            VK_L = 0x4C,
            VK_M = 0x4D,
            VK_N = 0x4E,
            VK_O = 0x4F,
            VK_P = 0x50,
            VK_Q = 0x51,
            VK_R = 0x52,
            VK_S = 0x53,
            VK_T = 0x54,
            VK_U = 0x55,
            VK_V = 0x56,
            VK_W = 0x57,
            VK_X = 0x58,
            VK_Y = 0x59,
            VK_Z = 0x5A,
            VK_OEM_1 = 0xBA,             //<- Vary according to Keyboard Layout. US: ";"      - BR: "ç"
            VK_OEM_PLUS = 0xBB,          //<- Same for ALL Keyboard Layout.     ALL: "= or +"
            VK_OEM_COMMA = 0xBC,         //<- Same for ALL Keyboard Layout.     ALL: ", or <"
            VK_OEM_MINUS = 0xBD,         //<- Same for ALL Keyboard Layout.     ALL: "- or _"
            VK_OEM_PERIOD = 0xBE,        //<- Same for ALL Keyboard Layout.     ALL: ". or >"
            VK_OEM_2 = 0xBF,             //<- Vary according to Keyboard Layout. US: "/ or ?" - BR: "; or :"
            VK_OEM_3 = 0xC0,             //<- Vary according to Keyboard Layout. US: "` or ~" - BR: "' or ""
            VK_OEM_4 = 0xDB,             //<- Vary according to Keyboard Layout. US: "[ or {" - BR: "´ or `"
            VK_OEM_5 = 0xDC,             //<- Vary according to Keyboard Layout. US: "\ or |" - BR: "] or }"
            VK_OEM_6 = 0xDD,             //<- Vary according to Keyboard Layout. US: "] or }" - BR: "[ or {"
            VK_OEM_7 = 0xDE,             //<- Vary according to Keyboard Layout. US: "' or "" - BR: "~ or ^"
            VK_OEM_8 = 0xDF,             //<- Vary according to Keyboard Layout. CA: "LEFT_CONTROL"
            VK_OEM_102 = 0xE2,           //<- Vary according to Keyboard Layout. EU: "\ or |" - BR: "\ or |"
            VK_PACKET = 0xE7,            //<- Used to pass Unicode characters as if they were keystrokes.
            VK_ATTN = 0xF6,              //<- ATTN_KEY       (Attention Key in the context of mainframe)
            VK_CRSEL = 0xF7,             //<- CRSEL_KEY      (Cursor Select Key)
            VK_EXSEL = 0xF8,             //<- EXSEL_KEY      (Extend Selection Key)
            VK_EREOF = 0xF9,             //<- ERASE_EOF_KEY  (Delete all characters from the current cursor position to the end of the field or line, in the context of mainframe)
            VK_PLAY = 0xFA,              //<- PLAY_KEY       (Alternative to VK_MEDIA_PLAY_PAUSE)
            VK_ZOOM = 0xFB,              //<- ZOOM_KEY       (Refers to a dedicated key found on certain specialized keyboards, most notably the Microsoft Natural Ergonomic Keyboard 4000)
            VK_NONAME = 0xFC,            //<- NO_NAME_KEY    (A blank-key of keyboard, which has no lettering, or it could be a generic, unbranded keyboard)
            VK_PA1 = 0xFD,               //<- PA1_KEY        (A special function key on legacy keyboards, especially used in IBM mainframes, and can also refer to a musical keyboard model)
            VK_BROWSER_BACK = 0xA6,      //<- MULTIMEDIA_KEY
            VK_BROWSER_FORWARD = 0xA7,   //<- MULTIMEDIA_KEY
            VK_BROWSER_REFRESH = 0xA8,   //<- MULTIMEDIA_KEY
            VK_BROWSER_STOP = 0xA9,      //<- MULTIMEDIA_KEY
            VK_BROWSER_SEARCH = 0xAA,    //<- MULTIMEDIA_KEY
            VK_BROWSER_FAVORITES = 0xAB, //<- MULTIMEDIA_KEY
            VK_BROWSER_HOME = 0xAC,      //<- MULTIMEDIA_KEY
            VK_VOLUME_MUTE = 0xAD,       //<- MULTIMEDIA_KEY
            VK_VOLUME_DOWN = 0xAE,       //<- MULTIMEDIA_KEY
            VK_VOLUME_UP = 0xAF,         //<- MULTIMEDIA_KEY
            VK_MEDIA_NEXT_TRACK = 0xB0,  //<- MULTIMEDIA_KEY
            VK_MEDIA_PREV_TRACK = 0xB1,  //<- MULTIMEDIA_KEY
            VK_MEDIA_STOP = 0xB2,        //<- MULTIMEDIA_KEY
            VK_MEDIA_PLAY_PAUSE = 0xB3,  //<- MULTIMEDIA_KEY (Alternative to PLAY_KEY)
            VK_LAUNCH_MAIL = 0xB4,       //<- MULTIMEDIA_KEY
        }

        public enum VirtualKeyInt
        {
            VK_ESCAPE = 27,              //<- ESC
            VK_TAB = 9,
            VK_SHIFT = 16,
            VK_LSHIFT = 160,
            VK_RSHIFT = 161,
            VK_CONTROL = 17,
            VK_LCONTROL = 162,
            VK_RCONTROL = 163,
            VK_MENU = 18,                //<- ALT
            VK_LMENU = 164,              //<- LEFT_ALT
            VK_RMENU = 165,              //<- RIGHT_ALT
            VK_CAPITAL = 20,             //<- CAPS_LOCK
            VK_NUMLOCK = 144,
            VK_SCROLL = 145,             //<- SCROLL_LOCK
            VK_RETURN = 13,              //<- ENTER
            VK_SPACE = 32,
            VK_PRIOR = 33,               //<- PAGE_UP
            VK_NEXT = 34,                //<- PAGE_DOWN
            VK_END = 35,
            VK_HOME = 36,
            VK_LEFT = 37,                //<- LEFT_ARROW
            VK_UP = 38,                  //<- UP_ARROW
            VK_RIGHT = 39,               //<- RIGHT_ARROW
            VK_DOWN = 40,                //<- DOWN_ARROW
            VK_SNAPSHOT = 44,            //<- PRINT_SCREEN
            VK_PAUSE = 19,
            VK_INSERT = 45,
            VK_DELETE = 46,
            VK_BACK = 8,                 //<- BACKSPACE
            VK_LWIN = 91,                //<- LEFT_WIN
            VK_RWIN = 92,                //<- RIGHT_WIN
            VK_APPS = 93,                //<- APPS_KEY/CONTEXT_MENU
            VK_NUMPAD0 = 96,             //<- If NumPad ON: "0" - If NumPad OFF: "INSERT"
            VK_NUMPAD1 = 97,             //<- If NumPad ON: "1" - If NumPad OFF: "END"
            VK_NUMPAD2 = 98,             //<- If NumPad ON: "2" - If NumPad OFF: "DOWN_ARROW"
            VK_NUMPAD3 = 99,             //<- If NumPad ON: "3" - If NumPad OFF: "PAGE_DOWN"
            VK_NUMPAD4 = 100,            //<- If NumPad ON: "4" - If NumPad OFF: "LEFT_ARROW"
            VK_NUMPAD5 = 101,            //<- If NumPad ON: "5" - If NumPad OFF: "?"
            VK_NUMPAD6 = 102,            //<- If NumPad ON: "6" - If NumPad OFF: "RIGHT_ARROW"
            VK_NUMPAD7 = 103,            //<- If NumPad ON: "7" - If NumPad OFF: "HOME"
            VK_NUMPAD8 = 104,            //<- If NumPad ON: "8" - If NumPad OFF: "UP_ARROW"
            VK_NUMPAD9 = 105,            //<- If NumPad ON: "9" - If NumPad OFF: "PAGE_UP"
            VK_MULTIPLY = 106,           //<- NUMPAD_* Can be used with NumPad ON or OFF
            VK_ADD = 107,                //<- NUMPAD_+ Can be used with NumPad ON or OFF
            VK_SEPARATOR = 108,          //<- NUMPAD_. Can be used with NumPad ON or OFF
            VK_SUBTRACT = 109,           //<- NUMPAD_- Can be used with NumPad ON or OFF
            VK_DECIMAL = 110,            //<- NUMPAD_, If NumPad ON: "," - If NumPad OFF: "DELETE"
            VK_DIVIDE = 111,             //<- NUMPAD_/ Can be used with NumPad ON or OFF
            VK_F1 = 112,
            VK_F2 = 113,
            VK_F3 = 114,
            VK_F4 = 115,
            VK_F5 = 116,
            VK_F6 = 117,
            VK_F7 = 118,
            VK_F8 = 119,
            VK_F9 = 120,
            VK_F10 = 121,
            VK_F11 = 122,
            VK_F12 = 123,
            VK_0 = 48,
            VK_1 = 49,
            VK_2 = 50,
            VK_3 = 51,
            VK_4 = 52,
            VK_5 = 53,
            VK_6 = 54,
            VK_7 = 55,
            VK_8 = 56,
            VK_9 = 57,
            VK_A = 65,
            VK_B = 66,
            VK_C = 67,
            VK_D = 68,
            VK_E = 69,
            VK_F = 70,
            VK_G = 71,
            VK_H = 72,
            VK_I = 73,
            VK_J = 74,
            VK_K = 75,
            VK_L = 76,
            VK_M = 77,
            VK_N = 78,
            VK_O = 79,
            VK_P = 80,
            VK_Q = 81,
            VK_R = 82,
            VK_S = 83,
            VK_T = 84,
            VK_U = 85,
            VK_V = 86,
            VK_W = 87,
            VK_X = 88,
            VK_Y = 89,
            VK_Z = 90,
            VK_OEM_1 = 186,              //<- Vary according to Keyboard Layout. US: ";"      - BR: "ç"
            VK_OEM_PLUS = 187,           //<- Same for ALL Keyboard Layout.     ALL: "= or +"
            VK_OEM_COMMA = 188,          //<- Same for ALL Keyboard Layout.     ALL: ", or <"
            VK_OEM_MINUS = 189,          //<- Same for ALL Keyboard Layout.     ALL: "- or _"
            VK_OEM_PERIOD = 190,         //<- Same for ALL Keyboard Layout.     ALL: ". or >"
            VK_OEM_2 = 191,              //<- Vary according to Keyboard Layout. US: "/ or ?" - BR: "; or :"
            VK_OEM_3 = 192,              //<- Vary according to Keyboard Layout. US: "` or ~" - BR: "' or ""
            VK_OEM_4 = 219,              //<- Vary according to Keyboard Layout. US: "[ or {" - BR: "´ or `"
            VK_OEM_5 = 220,              //<- Vary according to Keyboard Layout. US: "\ or |" - BR: "] or }"
            VK_OEM_6 = 221,              //<- Vary according to Keyboard Layout. US: "] or }" - BR: "[ or {"
            VK_OEM_7 = 222,              //<- Vary according to Keyboard Layout. US: "' or "" - BR: "~ or ^"
            VK_OEM_8 = 223,              //<- Vary according to Keyboard Layout. CA: "LEFT_CONTROL"
            VK_OEM_102 = 226,            //<- Vary according to Keyboard Layout. EU: "\ or |" - BR: "\ or |"
            VK_PACKET = 231,             //<- Used to pass Unicode characters as if they were keystrokes.
            VK_ATTN = 246,               //<- ATTN_KEY       (Attention Key in the context of mainframe)
            VK_CRSEL = 247,              //<- CRSEL_KEY      (Cursor Select Key)
            VK_EXSEL = 248,              //<- EXSEL_KEY      (Extend Selection Key)
            VK_EREOF = 249,              //<- ERASE_EOF_KEY  (Delete all characters from the current cursor position to the end of the field or line, in the context of mainframe)
            VK_PLAY = 250,               //<- PLAY_KEY       (Alternative to VK_MEDIA_PLAY_PAUSE)
            VK_ZOOM = 251,               //<- ZOOM_KEY       (Refers to a dedicated key found on certain specialized keyboards, most notably the Microsoft Natural Ergonomic Keyboard 4000)
            VK_NONAME = 252,             //<- NO_NAME_KEY    (A blank-key of keyboard, which has no lettering, or it could be a generic, unbranded keyboard)
            VK_PA1 = 253,                //<- PA1_KEY        (A special function key on legacy keyboards, especially used in IBM mainframes, and can also refer to a musical keyboard model)
            VK_BROWSER_BACK = 166,       //<- MULTIMEDIA_KEY
            VK_BROWSER_FORWARD = 167,    //<- MULTIMEDIA_KEY
            VK_BROWSER_REFRESH = 168,    //<- MULTIMEDIA_KEY
            VK_BROWSER_STOP = 169,       //<- MULTIMEDIA_KEY
            VK_BROWSER_SEARCH = 170,     //<- MULTIMEDIA_KEY
            VK_BROWSER_FAVORITES = 171,  //<- MULTIMEDIA_KEY
            VK_BROWSER_HOME = 172,       //<- MULTIMEDIA_KEY
            VK_VOLUME_MUTE = 173,        //<- MULTIMEDIA_KEY
            VK_VOLUME_DOWN = 174,        //<- MULTIMEDIA_KEY
            VK_VOLUME_UP = 175,          //<- MULTIMEDIA_KEY
            VK_MEDIA_NEXT_TRACK = 176,   //<- MULTIMEDIA_KEY
            VK_MEDIA_PREV_TRACK = 177,   //<- MULTIMEDIA_KEY
            VK_MEDIA_STOP = 178,         //<- MULTIMEDIA_KEY
            VK_MEDIA_PLAY_PAUSE = 179,   //<- MULTIMEDIA_KEY (Alternative to PLAY_KEY)
            VK_LAUNCH_MAIL = 180,        //<- MULTIMEDIA_KEY
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        public static bool isNumLockCurrentlyEnabled()
        {
            return Keyboard.IsKeyToggled(Key.NumLock);
        }

        public static bool isCapsLockCurrentlyEnabled()
        {
            return Keyboard.IsKeyToggled(Key.CapsLock);
        }

        public static bool isScrollLockCurrentlyEnabled()
        {
            return Keyboard.IsKeyToggled(Key.Scroll);
        }

        public static void DoKeyDownAndUpSingleKeyboardPress(VirtualKeyHex virtualKey)
        {
            keybd_event((byte)virtualKey, 0x45, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event((byte)virtualKey, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }

        public static bool isKeyDownNowKeyboardPress(string simulationKeyHoldingId)
        {
            bool toReturn = false;

            if (simulationKeyHoldingThreads == null)
                simulationKeyHoldingThreads = new Dictionary<string, KeyHandleThread>();

            if (simulationKeyHoldingThreads.ContainsKey(simulationKeyHoldingId) == true)
                toReturn = true;

            return toReturn;
        }

        public static void DoKeyDownKeyboardPress(string simulationKeyHoldingId, VirtualKeyHex virtualKeyToHold)
        {
            if (isKeyDownNowKeyboardPress(simulationKeyHoldingId) == true)
                return;

            KeyHandleThread keyHandleThread = new KeyHandleThread();
            keyHandleThread.cancellationTokenSource = new CancellationTokenSource();
            keyHandleThread.simulationHoldingThread = new Thread((object param1) =>
            {
                CancellationToken cancellationToken = (CancellationToken)param1;
                System.Windows.Threading.Dispatcher currentApplicationDispatcher = Application.Current.Dispatcher;
                Action keyPressActionToRun = () => { keybd_event((byte)virtualKeyToHold, 0x0, 0, 0); };
                while (true)
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested == true)
                            break;
                        currentApplicationDispatcher.Invoke(keyPressActionToRun);
                        Thread.Sleep(16);
                    }
                    catch (Exception e) { break; }
                }
            });
            keyHandleThread.simulationHoldingThread.Start(keyHandleThread.cancellationTokenSource.Token);

            simulationKeyHoldingThreads.Add(simulationKeyHoldingId, keyHandleThread);
        }

        public static void DoKeyDownKeyboardPress(string simulationKeyHoldingId, VirtualKeyHex virtualKeyToHold, int setDelayToAutoDoKeyUpInMs)
        {
            if (isKeyDownNowKeyboardPress(simulationKeyHoldingId) == true)
                return;

            DoKeyDownKeyboardPress(simulationKeyHoldingId, virtualKeyToHold);

            Thread thread = new Thread((object param1) =>
            {
                string[] dataStrings = ((string)param1).Split("₢");
                string id = dataStrings[0];
                int delay = int.Parse(dataStrings[1]);
                Thread.Sleep(delay);
                if (isKeyDownNowKeyboardPress(id) == true)
                    DoKeyUpKeyboardPress(id);
            });
            thread.Start((simulationKeyHoldingId + "₢" + setDelayToAutoDoKeyUpInMs));
        }

        public static void DoKeyUpKeyboardPress(string simulationKeyHoldingId)
        {
            if (isKeyDownNowKeyboardPress(simulationKeyHoldingId) == false)
                return;

            simulationKeyHoldingThreads[simulationKeyHoldingId].cancellationTokenSource.Cancel();

            simulationKeyHoldingThreads.Remove(simulationKeyHoldingId);
        }

        public static void DoKeyHotkeyKeyboardPress(VirtualKeyHex virtualKey1, VirtualKeyHex virtualKey2)
        {
            keybd_event((byte)virtualKey1, 0, KEYEVENTF_KEYDOWN, 0);
            keybd_event((byte)virtualKey2, 0, KEYEVENTF_KEYDOWN, 0);
            keybd_event((byte)virtualKey2, 0, KEYEVENTF_KEYUP, 0);
            keybd_event((byte)virtualKey1, 0, KEYEVENTF_KEYUP, 0);
        }

        public static void DoKeyHotkeyKeyboardPress(VirtualKeyHex virtualKey1, VirtualKeyHex virtualKey2, VirtualKeyHex virtualKey3)
        {
            keybd_event((byte)virtualKey1, 0, KEYEVENTF_KEYDOWN, 0);
            keybd_event((byte)virtualKey2, 0, KEYEVENTF_KEYDOWN, 0);
            keybd_event((byte)virtualKey3, 0, KEYEVENTF_KEYDOWN, 0);
            keybd_event((byte)virtualKey3, 0, KEYEVENTF_KEYUP, 0);
            keybd_event((byte)virtualKey2, 0, KEYEVENTF_KEYUP, 0);
            keybd_event((byte)virtualKey1, 0, KEYEVENTF_KEYUP, 0);
        }

        #endregion

        #region KeyboardWatcher

        public class KeyboardKeys_Watcher
        {
            //Private constants
            private const int WH_KEYBOARD_LL = 13;
            private const int WM_KEYDOWN = 0x0100;
            private const int WM_KEYUP = 0x0101;

            //Private variables
            private LowLevelKeyboardProc procedure = null;
            private IntPtr hookId = IntPtr.Zero;
            private bool isDisposed = false;

            //Public callbacks

            public event Action<int> OnPressKeys;

            //Import methods

            private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr GetModuleHandle(string lpModuleName);

            //Core methods

            public KeyboardKeys_Watcher()
            {
                //Store the callback into a strong reference
                procedure = HookCallback;

                //Register the callback to the low level keys, of windows hook
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    hookId = SetWindowsHookEx(WH_KEYBOARD_LL, procedure, GetModuleHandle(curModule.ModuleName), 0);
                }
            }

            private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
            {
                //If the key code is major than zero, continues to process
                if (nCode >= 0)
                    if (wParam == (IntPtr)WM_KEYDOWN)   //<- (for more performance, if is a keyboard key pressed down event, send callback)
                        if (OnPressKeys != null)
                            OnPressKeys(Marshal.ReadInt32(lParam));

                return CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            public void Dispose()
            {
                //If is not disposed, dispose of this object
                if (isDisposed == false)
                {
                    //Remove all low level hooks
                    UnhookWindowsHookEx(hookId);

                    //Clean variables
                    procedure = null;
                    hookId = IntPtr.Zero;
                }
                isDisposed = true;
            }
        }

        public class KeyboardHotkey_Interceptor : IDisposable
        {
            //Private variables
            private WindowInteropHelper host;
            private int identifier;
            private bool isDisposed = false;

            //Public enums
            [Flags]
            public enum ModifierKeyCodes : uint
            {
                None = 0,
                Alt = 1,
                Control = 2,
                Shift = 4,
                Windows = 8
            }

            //Public variables
            public Window window;
            public ModifierKeyCodes modifier;
            public VirtualKeyInt key;

            //Public callbacks
            public event Action OnPressHotkey;

            //Import methods

            [DllImport("user32.dll")]
            public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

            [DllImport("user32.dll")]
            public static extern bool RegisterHotKey(IntPtr hWnd, int id, ModifierKeyCodes fdModifiers, VirtualKeyInt vk);

            //Core methods

            public KeyboardHotkey_Interceptor(Window window, int id, ModifierKeyCodes modifierCode, VirtualKeyInt keyCode)
            {
                //Store the information
                this.window = window;
                this.modifier = modifierCode;
                this.key = keyCode;

                //Prepare the host and identifier of this hotkey registration
                host = new WindowInteropHelper(this.window);
                identifier = (this.window.GetHashCode() + id);

                //Register the hotkey
                RegisterHotKey(host.Handle, identifier, this.modifier, this.key);

                //Register the callback with a pre-process logic
                ComponentDispatcher.ThreadPreprocessMessage += ProcessMessage;
            }

            void ProcessMessage(ref MSG msg, ref bool handled)
            {
                //Validate the response
                if ((msg.message == 786) && (msg.wParam.ToInt32() == identifier) && (OnPressHotkey != null))
                    OnPressHotkey();
            }

            public void ForceOnPressHotkeyEvent()
            {
                //Force the execution of the event "OnPressHotkey"
                if (OnPressHotkey != null)
                    OnPressHotkey();
            }

            public void Dispose()
            {
                //If is not disposed, dispose of this object
                if (isDisposed == false)
                {
                    //Unregister callback pre-process logic
                    ComponentDispatcher.ThreadPreprocessMessage -= ProcessMessage;

                    //Unregister the hotkey
                    UnregisterHotKey(host.Handle, identifier);
                    this.window = null;
                    host = null;
                }
                isDisposed = true;
            }
        }

        #endregion
    }
}