using Avalonia.Controls;
using Avalonia.CopySettings.Models;
using Avalonia.CopySettings.Views;
using Avalonia.Threading;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Avalonia.CopySettings.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ICommand FolderDialogCommand { get; }
        public ICommand CopyCommand { get; }

        private ObservableCollection<Character> CopyFromCollection { get; set; } = new ObservableCollection<Character>();
        private ObservableCollection<Character> CopyToCollection { get; set; } = new ObservableCollection<Character>();

        private string? backupPath;

        private string? folderPathText;

        public MainWindowViewModel()
        {
            CopyCommand = ReactiveCommand.Create(() =>
            {
                // Code here will be executed when the button is clicked.
            });

            FolderDialogCommand = ReactiveCommand.Create(async () =>
            {
                OpenFolderDialog folderDialog = new()
                {
                    Directory = ResolvePath()
                };

                string result = await folderDialog.ShowAsync(MainWindow.Instance);
                FolderPathText = result;
                await Task.Run(() => GetFiles(result));
            });
        }

        public string? FolderPathText
        {
            get => folderPathText;
            set => this.RaiseAndSetIfChanged(ref folderPathText, value);
        }

        public string ResolvePath()
        {
            //Navigates to ccp\eve in localappdata
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string eveFolder = $"{localData}\\CCP\\EVE\\";
            string[] eveFolderList = Directory.GetDirectories(eveFolder);
            foreach (string eveFolderItem in eveFolderList)
            {
                //Looks for tranquility foldername varies by eve install location
                if (eveFolderItem.EndsWith("_eve_sharedcache_tq_tranquility"))
                {
                    string tranqFolder = System.IO.Path.Combine(eveFolder, eveFolderItem);
                    string[] tranqFolderList = Directory.GetDirectories(tranqFolder);
                    backupPath = tranqFolder;
                    foreach (string tranqFolderItem in tranqFolderList)
                    {
                        //Looks for settings_Default folder
                        if (tranqFolderItem.EndsWith("Default"))
                        {
                            string settingsPath = System.IO.Path.Combine(tranqFolder, tranqFolderItem);
                            return settingsPath;
                        }
                    }
                    //If default forlder not found direct to tranq folder
                    return tranqFolder;
                }
            }
            //If neither default or tranq folder not found return eve folder
            return eveFolder;
        }

        public async void GetFiles(string dir)
        {
            await Task.Run(() =>
            {
                //Gets character files from folder
                ClearCollection();
                string[] characterFiles = Directory.GetFiles(dir);
                foreach (string characterFilePath in characterFiles)
                {
                    //Looks for core_char files
                    string characterFile = System.IO.Path.GetFileName(characterFilePath);
                    if (characterFile.StartsWith("core_char_"))
                    {
                        string characterID = PathToID(characterFile);
                        if (!string.IsNullOrEmpty(characterID))
                        {
                            GetCharacter(characterID, characterFilePath);
                        }
                        continue;
                    }
                }
            });
        }

        private void GetCharacter(string characterid, string characterfilepath)
        {
            dynamic? json = JsonHandler($"https://esi.evetech.net/latest/characters/{characterid}/?datasource=tranquility");
            string? characterName = json.name;
            Character character = new()
            {
                CharacterName = characterName,
                CharacterId = characterid,
                CharacterFilePath = characterfilepath
            };
            AddCharacter(character);
        }

        private void AddCharacter(Character character)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                CopyFromCollection.Add(character);
                CopyToCollection.Add(character);
            });
        }

        private void ClearCollection()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                CopyFromCollection.Clear();
                CopyToCollection.Clear();
            });
        }

        private static dynamic? JsonHandler(string url)
        {
            try
            {
                string json = new WebClient().DownloadString(url);
                dynamic? jsonToObject = JsonConvert.DeserializeObject(json);
                return jsonToObject;
            }
            catch (System.Net.WebException e)
            {
                Console.WriteLine($"Failed to connect ESI'{e}'");
                return null;
            }
        }

        private static string PathToID(string filePath)
        {
            //parses filepath to id
            string filePathToTrim = System.IO.Path.GetFileName(filePath);
            string characterID = filePathToTrim.Trim('c', 'o', 'r', 'e', '_', 'h', 'a', 'r', '.', 'd', 'a', 't');
            return characterID;
        }
    }
}