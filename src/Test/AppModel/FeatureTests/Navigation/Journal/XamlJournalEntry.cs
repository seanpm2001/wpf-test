// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using System.Data;
using System.Windows.Media;
using System.Windows.Controls;
using System.Collections;
using Microsoft.Test.Logging;
using Microsoft.Test.Globalization;

namespace Microsoft.Windows.Test.Client.AppSec.Navigation
{
    // XamlJournalEntryApp

    /// <summary>
    /// BVT that has sets JournalEntry.Name property for Pages in Markup
    /// Verifies that actual journal entries match expected journal entries
    /// with journal entries contains spaces / different language characters
    /// and characters other that (letters, digits and underscore)
    /// To prevent Bug  from regressing
    /// </summary>
    public partial class NavigationTests : Application
    {
        #region XamlJournalEntry globals
        NavigationWindow _xamlJournalEntry_navWin = null;
        String _xamlJournalEntry_Page1JournalEntry = "This is Page 1 * / ^";
        String _xamlJournalEntry_Page2JournalEntry = "づヌナバあニ";
        String _xamlJournalEntry_Page3JournalEntry = "Mixed ٦۵۰حصي";
        #endregion

        enum XamlJournalEntry_CurrentTest
        {
            UnInit,
            Init,
            NavToPage2,
            NavToPage3,
            NavToString,
            End
        };

        XamlJournalEntry_CurrentTest _xamlJournalEntry_currentState = XamlJournalEntry_CurrentTest.UnInit;

        void XamlJournalEntry_Startup(object sender, StartupEventArgs e)
        {
            NavigationHelper.CreateLog("XamlJournalEntry");
            Application.Current.StartupUri = new Uri("XamlJournalEntry_Page1.xaml", UriKind.RelativeOrAbsolute); 

            _xamlJournalEntry_currentState = XamlJournalEntry_CurrentTest.Init;
            Application.Current.Navigated += new NavigatedEventHandler(XamlJournalEntry_Navigated);
            NavigationHelper.SetStage(TestStage.Run);
        }

        void XamlJournalEntry_Navigated(object sender, NavigationEventArgs e)
        {
            switch (_xamlJournalEntry_currentState)
            {
                case XamlJournalEntry_CurrentTest.Init:
                    _xamlJournalEntry_navWin = Application.Current.MainWindow as NavigationWindow;
                    _xamlJournalEntry_navWin.ContentRendered += new EventHandler(XamlJournalEntry_ContentRendered);
                    break;
            }
        }

        void XamlJournalEntry_ContentRendered(object sender, EventArgs e)
        {
            switch (_xamlJournalEntry_currentState)
            {
                case XamlJournalEntry_CurrentTest.Init:
                    _xamlJournalEntry_currentState = XamlJournalEntry_CurrentTest.NavToPage2;
                    _xamlJournalEntry_navWin.Navigate(new Uri("XamlJournalEntry_Page2.xaml", UriKind.RelativeOrAbsolute));
                    break;

                case XamlJournalEntry_CurrentTest.NavToPage2:
                    // verify that back stack contains Journal Entry for Page 1
                    XamlJournalEntry_VerifyBackEntry(_xamlJournalEntry_Page1JournalEntry);
                    _xamlJournalEntry_currentState = XamlJournalEntry_CurrentTest.NavToPage3;
                    _xamlJournalEntry_navWin.Navigate(new Uri("XamlJournalEntry_Page3.xaml", UriKind.RelativeOrAbsolute));
                    break;

                case XamlJournalEntry_CurrentTest.NavToPage3:
                    // verify that back stack contains Journal Entry for Page 2
                    XamlJournalEntry_VerifyBackEntry(_xamlJournalEntry_Page2JournalEntry);
                    _xamlJournalEntry_currentState = XamlJournalEntry_CurrentTest.NavToString;
                    _xamlJournalEntry_navWin.Navigate("This is some string");
                    break;

                case XamlJournalEntry_CurrentTest.NavToString:
                    // verify that back stack contains Journal Entry for Page 3
                    XamlJournalEntry_VerifyBackEntry(_xamlJournalEntry_Page3JournalEntry);
                    _xamlJournalEntry_currentState = XamlJournalEntry_CurrentTest.End;
                    _xamlJournalEntry_navWin.Navigate("End");
                    break;

                case XamlJournalEntry_CurrentTest.End:
                    // verify that back stack entry is "untitled"
                    string untitled = "Untitled";
                    Assembly presentationFramework = null;
            
                    try
                    {
                        presentationFramework = Assembly.Load("PresentationFramework, Version=3.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                        untitled = Extract.GetExceptionString("Untitled", presentationFramework);
                    }
                    catch (Exception ex)
                    {
                        NavigationHelper.Output("Couldn't get resource string for Untitled: " + ex.ToString());
                    }

                    XamlJournalEntry_VerifyBackEntry(untitled);
                    NavigationHelper.Pass("All subtests passed!");
                    break;
            }
        }

        void XamlJournalEntry_VerifyBackEntry(String journalEntry)
        {
            IEnumerator backStackEnumerator = NavigationUtilities.GetBackStack(_xamlJournalEntry_navWin);
            backStackEnumerator.MoveNext();
            JournalEntry backTopEntry = backStackEnumerator.Current as JournalEntry;
            String actualName = backTopEntry.Name;
            NavigationHelper.Output("Top backstack entry = " + actualName);
            if (journalEntry.Equals(actualName))
            {
                NavigationHelper.Output("Journal entry names match");
            }
            else
            {
                NavigationHelper.Fail("Journal entry names don't match Actual = " + actualName
                            + " expected = " + journalEntry);
            }
        }
    }
}