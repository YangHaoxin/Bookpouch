﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace Bookpouch
{    
    /// <summary>
    /// Tools for editing and manipulating single books in the library
    /// </summary>
    static class BookKeeper
    {
        /// <summary>
        /// Add a new book into the library
        /// </summary>
        /// <param name="file">Path to the file which is being added</param>
        public static void Add(string file) //Add a new book into the library
        {
            DebugConsole.WriteLine("Book keeper: New book is being manually added into the library: " + file);

            if(!Directory.Exists(Properties.Settings.Default.BooksDir))
                throw new DirectoryNotFoundException("Root directory in which the book files are stored was not found.");            

            var finfo = new FileInfo(file);
            var supportedExtensions = Properties.Settings.Default.FileExtensions.Split(';');

            if (!supportedExtensions.Contains(finfo.Extension.Substring(1), StringComparer.CurrentCultureIgnoreCase))
                //Only allow files with supported extensions
                return;

            if (!File.Exists(file))
                throw new FileNotFoundException("The book file supplied for adding into the library doesn't exist.");

            var dirName = Path.GetFileNameWithoutExtension(finfo.Name);
                //Name of the directory, in which the book file will be stored, name of the directory is identical to the file name, except without extension
            var dirPath = Path.Combine(Properties.Settings.Default.BooksDir, dirName);
            var newDirPath = dirPath;            
            int copyNumber;

            for(copyNumber = 1; Directory.Exists(newDirPath); copyNumber++) //If the folder already exists append a number to the new folder's name
                newDirPath = dirPath + " (" + copyNumber + ")";

            dirPath = newDirPath; //We are now sure, the folder name for the book storing folder doesn't already exist

            var fileName = Path.GetFileNameWithoutExtension(finfo.Name)  + (copyNumber > 1 ? " (" + (copyNumber - 1) + ")" : "") + finfo.Extension; //If the parent directory got a number added to its name, add it to the book file name as well
            var path = Path.Combine(dirPath, fileName);

            try
            {
                Directory.CreateDirectory(dirPath); //Create the dir in the default book folder, specified in the settings        
                finfo.CopyTo(path, true);
                GenerateData(path); //Generate data for this book 
            }
            catch (Exception e)
            {
                MainWindow.Info(String.Format(UiLang.Get("BookCopyError"), file), 1);
                DebugConsole.WriteLine("Book keeper: Copying of the book file " + file + " failed because: " + e);

            }            
        }        

        /// <summary>
        /// Generate *.dat file containing information about a book (mostly extracted from the book file) and save it into the book's folder.
        /// </summary>
        /// <param name="bookFile">Path to the book file</param>
        /// <exception cref="FileNotFoundException">The supplied book file was not found</exception>
        public static void GenerateData(string bookFile) 
        {
            if (!File.Exists(bookFile))
            {
                DebugConsole.WriteLine("Book keeper: Generating book data failed, because the supplied book file (" + bookFile + ") doesn't exist.");
                throw new FileNotFoundException();
            }

            DebugConsole.WriteLine("Book keeper: Generating data for " + bookFile);
            var bookFileRelativePath = GetRelativeBookFilePath(bookFile);

            var finfo = new FileInfo(bookFile);
            var bookPeek = new BookPeek(finfo);

            Db.NonQuery("INSERT OR IGNORE INTO books VALUES(@Path, @Title, @Author, @Contributor, @Publisher, @Language, @Published, @Description, @Series, @Coverage, @MobiType, @Identifier, @Relation, @Size, @Favorite, @Sync, @Created, @Cover)", 
                new[]
                {
                    new SQLiteParameter("Path", bookFileRelativePath), 
                    new SQLiteParameter("Title", bookPeek.List["title"].ToString()), 
                    new SQLiteParameter("Author", (bookPeek.List.ContainsKey("author") ? bookPeek.List["author"] : String.Empty).ToString()), 
                    new SQLiteParameter("Contributor", (bookPeek.List.ContainsKey("contributor") ? bookPeek.List["contributor"] : String.Empty).ToString()), 
                    new SQLiteParameter("Publisher", (bookPeek.List.ContainsKey("publisher") ? bookPeek.List["publisher"] : String.Empty).ToString()), 
                    new SQLiteParameter("Language", (bookPeek.List.ContainsKey("language") ? bookPeek.List["language"] : String.Empty).ToString()),
                    new SQLiteParameter("Published",  (bookPeek.List.ContainsKey("published") ? ((DateTime) bookPeek.List["published"]).ToString("yyyy-MM-dd") : null)),
                    new SQLiteParameter("Description", (bookPeek.List.ContainsKey("description") ? bookPeek.List["description"] : String.Empty).ToString()), 
                    new SQLiteParameter("Series", String.Empty),
                    new SQLiteParameter("Coverage", (bookPeek.List.ContainsKey("coverage") ? bookPeek.List["coverage"] : String.Empty).ToString()), 
                    new SQLiteParameter("MobiType", (bookPeek.List.ContainsKey("type") ? bookPeek.List["type"] : String.Empty).ToString()),
                    new SQLiteParameter("Identifier", (bookPeek.List.ContainsKey("identifier") ? bookPeek.List["identifier"] : String.Empty).ToString()), 
                    new SQLiteParameter("Relation", (bookPeek.List.ContainsKey("relation") ? bookPeek.List["relation"] : String.Empty).ToString()), 
                    new SQLiteParameter("Size",  (ulong) finfo.Length), 
                    new SQLiteParameter("Favorite", false),
                    new SQLiteParameter("Sync", false), 
                    new SQLiteParameter("Created", DateTime.Now.ToString("yyyy-MM-dd")), 
                    new SQLiteParameter("Cover", (bookPeek.List.ContainsKey("cover") ? bookPeek.List["cover"] : null))
                });     
       
            if(!bookPeek.List.ContainsKey("categories"))
                return;
            

            var categories = (List<string>) bookPeek.List["categories"];
            var categoryInserts = new List<string>();
            var categoryParameters = new List<SQLiteParameter> { new SQLiteParameter("Path", bookFileRelativePath) };
            var i = 0;

            foreach (var category in categories)
            {
                i++;
                categoryInserts.Add("(@Path, @Category" + i + ", 1)");    
                categoryParameters.Add(new SQLiteParameter("Category" + i, category));
            }

            var sqlCategories =  String.Join(",", categoryInserts);
            
            Db.NonQuery("INSERT OR IGNORE INTO categories VALUES " + sqlCategories, categoryParameters.ToArray());
        }

        /// <summary>
        /// Read the info file associated with the supplied book and return the information
        /// </summary>
        /// <param name="bookFile">Path to the book file</param>
        /// <returns>Dictionary containing saved information about the file</returns>
        /// <exception cref="FileNotFoundException">Book file associated with the .dat file doesn't exist or vice versa</exception>
        /// <exception cref="RowNotInTableException">Row containing info about the given book was not found in the table and the attempt at regenerating this row failed.</exception>
        public static BookData GetData(string bookFile)
        {
            if (!File.Exists(bookFile))
                throw new FileNotFoundException();
            
            const string sql = "SELECT * FROM books WHERE Path = @Path LIMIT 1";
            var bookFileRelativePath = GetRelativeBookFilePath(bookFile);
            var parameters = new[] {new SQLiteParameter("Path", bookFileRelativePath)};
            var query = Db.Query(sql, parameters);
                        
            if (!query.HasRows) //If the row is missing, attempt to generate it 
            {                
                query.Dispose();
                DebugConsole.WriteLine("Book keeper: Nonexistent data for " + bookFile + ". Triggering data generation...");
                GenerateData(bookFile);
                
                query = Db.Query(sql, parameters);

                if (!query.HasRows)
                {
                    query.Dispose();
                    throw new RowNotInTableException(
                        "Row for the specified book file was not found in the database, and the attempted regeneration failed.");
                }
            }           

            query.Read();                       
            
            var bookData = CastSqlBookRowToBookData(query);
           
            query.Dispose();

            var queryCategories = Db.Query("SELECT Name FROM categories WHERE Path = @Path", parameters);

            while (queryCategories.Read())
                bookData.Categories.Add(queryCategories["Name"].ToString());

            return bookData;
        }

        /// <summary>
        /// Casts book row from a sql query to a BookData object
        /// </summary>
        /// <param name="query"></param>
        /// <returns>Filled in BookData object</returns>
        public static BookData CastSqlBookRowToBookData(SQLiteDataReader query)
        {            
            var bookData = new BookData
            {
                Title = (string) query["Title"],
                Author = (string) query["Author"],
                Contributor = (string)query["Contributor"],
                Publisher = (string) query["Publisher"],                
                Language = (string) query["Language"],
                Published = (DateTime?) (query["Published"].ToString() != String.Empty ? query["Published"] : null),
                Description = (string) query["Description"],
                Series = (string) query["Series"],
                Coverage = (string)query["Coverage"],
                Created = (DateTime)query["Created"],
                Size = Convert.ToUInt64(query["Size"]),
                Favorite = (bool) query["Favorite"],
                Sync = (bool) query["Sync"],                
                Cover = (byte[]) (query["Cover"].ToString() != String.Empty ? query["Cover"] : null),
                Path = GetAbsoluteBookFilePath((string) query["Path"])
            };
         
            return bookData;
        }

        /// <summary>
        /// Save dictionary with book info back into a file
        /// </summary>
        /// <param name="bookData">The dictionary object containing the book info</param>
        /// <exception cref="FileNotFoundException">Supplied book file was not found</exception>
        /// <exception cref="RowNotInTableException">Database record for the supplied book file doesn't exists and it was not possible to regenerate it</exception>
        public static void SaveData(BookData bookData)
        {
            bookData.Path = GetRelativeBookFilePath(bookData.Path);
            const string sql = "SELECT EXISTS( SELECT * FROM books WHERE Path = @Path LIMIT 1)";
            var parameters = new[] { new SQLiteParameter("Path", bookData.Path) };
            var exists = Db.QueryExists(sql, parameters);
            
            if (!exists)
            {
                GenerateData(bookData.Path);

                exists = Db.QueryExists(sql, parameters);

                if(!exists)
                    throw new RowNotInTableException("Row for the specified book file was not found in the database, and the attempted regeneration failed.");
            }
           
            Db.NonQuery(
              "UPDATE books SET Title = @Title, Author = @Author, Contributor = @Contributor, Publisher = @Publisher, Language = @Language, Published = @Published, Description = @Description, Series = @Series, Coverage = @Coverage, Size = @Size, Favorite = @Favorite, Sync = @Sync, Cover = @Cover WHERE Path = @Path",
                typeof (BookData).GetFields().Select(property => new SQLiteParameter(property.Name, property.GetValue(bookData))).ToArray());                
        }

        /// <summary>
        /// Remove a book from the library
        /// </summary>
        /// <param name="bookFile">Path to the folder which contains the book files</param>
        public static void Discard(string bookFile) //Permanently remove a book from the library
        {
            if (!File.Exists(bookFile))
                return;

            const string sqlDeleteBook = "DELETE FROM books WHERE Path = @Path";
            const string sqlDeleteCategories = "DELETE FROM categories WHERE Path = @Path";

            var bookFileRelativePath = GetRelativeBookFilePath(bookFile);

            try
            {
                File.Delete(bookFile);

                Db.NonQuery(sqlDeleteBook, new[]{ new SQLiteParameter("Path", bookFileRelativePath)});
                Db.NonQuery(sqlDeleteCategories, new[] { new SQLiteParameter("Path", bookFileRelativePath) });

                LibraryStructure.GenerateFileTree();
                MainWindow.MW.BookGridReload();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                MainWindow.Info(String.Format(UiLang.Get("DiscardingBookFailed"), bookFile));
                DebugConsole.WriteLine("Book keeper: I was unable to delete " + bookFile + ": " + e);
            }
        }

        /// <summary>
        /// If relative path to a book file is given, it will be returned unchanged, if absolute path to a book file is given, it will be turned into a relative path and returned.
        /// This only works with book files placed within the specified library root directory
        /// </summary>
        /// <param name="path">Relative or absolute path to a book file from the library</param>
        /// <returns>Relative path to the book file</returns>
        public static string GetRelativeBookFilePath(string path)
        {
            return path.Replace(Properties.Settings.Default.BooksDir + Path.DirectorySeparatorChar, String.Empty);            
        }

        /// <summary>
        /// If absolute path to a book file is given, it will be returned unchanged, if relative path to a book file is given, it will be turned into a absolute  path and returned.
        /// This only works with book files placed within the specified library root directory
        /// </summary>
        /// <param name="path">Relative or absolute path to a book file from the library</param>
        /// <returns>Absolute path to the book file</returns>
        public static string GetAbsoluteBookFilePath(string path)
        {
            return (path.StartsWith(Properties.Settings.Default.BooksDir)
                ? path
                : Path.Combine(Properties.Settings.Default.BooksDir, path));
        }
    }
    
}
