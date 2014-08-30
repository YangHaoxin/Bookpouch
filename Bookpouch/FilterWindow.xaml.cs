﻿#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

#endregion

namespace Bookpouch
{
    /// <summary>
    /// Interaction logic for EditBook.xaml, containing the form for editing book details
    /// </summary>
    public partial class FilterWindow
    {
        public FilterWindow()
        {            
            InitializeComponent();
        }


        private void Language_OnLoaded(object sender, RoutedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            var cultureList = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
            var languageOptions = cultureList.Select(culture => new Settings.LanguageOption(culture)).ToList();

            comboBox.ItemsSource = languageOptions;   
        }


        /// <summary>
        /// Save the language in which the book is written
        /// </summary>        
        private void Language_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            var language = (Settings.LanguageOption)comboBox.SelectedItem;

            FilterSet("Language", language.CultureInfo.Name);
        }


        

        /// <summary>
        /// Handle saving  values for all checkboxes
        /// </summary>        
        private void CheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            var box = (CheckBox)sender;
            FilterSet(box.Name, box.IsChecked);
        }


        /// <summary>
        /// Handle saving  values for all  textboxes
        /// </summary>        
        private void TextBox_OnChanged(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            FilterSet(textBox.Name, textBox.Text);
        }

        /// <summary>
        /// Handle saving values for all  dateboxes
        /// </summary>        
        private void DatePicker_OnChanged(object sender, RoutedEventArgs e)
        {
            var datePicker = (DatePicker)sender;
            FilterSet(datePicker.Name, datePicker.SelectedDate);
        }

        private void Series_OnLoaded(object sender, RoutedEventArgs e)
        {
            var bookData = LibraryStructure.List();
            var hintSet = new HashSet<string>();

            foreach (var info in bookData.Where(info => info.Series != ""))
                hintSet.Add(info.Series);

            var hintList = hintSet.ToList();
            hintList.Sort();

            new Whisperer
            {
                TextBox = (TextBox)sender,
                HintList = hintList
            };
        }

        readonly ObservableCollection<EditBook.CategoryTag> categoryTagList = new ObservableCollection<EditBook.CategoryTag>();

        private void Category_OnLoaded(object sender, RoutedEventArgs e)
        {      
            var defaultCategories = Properties.Settings.Default.DefaultCategories.Split(';');
            var hintList = new List<string>(defaultCategories);
            hintList.AddRange(LibraryStructure.CategoryList());
            hintList.Sort();

            new Whisperer
            {
                TextBox = (TextBox)sender,
                HintList = hintList
            };
        }

        /// <summary>
        /// Remove category from the book
        /// </summary>
        private void CategoryTag_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var categoryTag = (EditBook.CategoryTag)(((Button)sender).DataContext);

            categoryTagList.Remove(categoryTag);

            if (categoryTagList.Count == 0)
                CategoryTagsBorder.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Add category to the book
        /// </summary>        
        private void Category_OnKeyUp(object sender, KeyEventArgs e)
        {
            var textBox = (TextBox)sender;

            if (textBox.Text.Length <= 1 || (textBox.Text.Substring(textBox.Text.Length - 1) != ";" && textBox.Text.Substring(textBox.Text.Length - 1) != ","))
                return;

            var category = textBox.Text.Substring(0, textBox.Text.Length - 1);
            textBox.Text = String.Empty;

            if (categoryTagList.Any(categoryTag => categoryTag.Name == category))
                return;

            CategoryTagsBorder.Visibility = Visibility.Visible;
            categoryTagList.Add(new EditBook.CategoryTag { Name = category });
        }


        /// <summary>
        /// Save search parameters into the filter list
        /// </summary>
        /// <param name="key">Name of the filter field to which to save the data</param>
        /// <param name="value">Value to be saved into the specified field</param>
        private void FilterSet(string key, object value)
        {
            var type = typeof (MainWindow.BookFilter);

            if(key == null || type.GetField(key) == null)
                return;

            type.GetField(key).SetValue(MainWindow.MW.Filter, value);
        }


    }
}
