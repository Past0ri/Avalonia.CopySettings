using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.CopySettings.Models;
using Avalonia.CopySettings.Views;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Avalonia.CopySettings.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private static readonly HttpClient s_httpClient = new();
        private string? backupPath;
        private string? folderPathText;

        public MainWindowViewModel()
        {
            CopyCommand = ReactiveCommand.Create(async () =>
            {
                await Task.Run(() => CopyCharacterFiles());
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

        public ICommand CopyCommand { get; }
        public ICommand FolderDialogCommand { get; }

        public string? FolderPathText
        {
            get => folderPathText;
            set => this.RaiseAndSetIfChanged(ref folderPathText, value);
        }

        public AvaloniaList<object>? ToSelectedItems { get; set; }
        private AvaloniaList<Character> CopyFromCollection { get; set; } = new AvaloniaList<Character>();
        private AvaloniaList<Character> CopyToCollection { get; set; } = new AvaloniaList<Character>();
        private Character? FromSelectedItem { get; set; }

        public static async Task<Bitmap> LoadPotrait(string characterid)
        {
            await using var imageStream = await LoadPotraitBitmapAsync(characterid);
            return await Task.Run(() => Bitmap.DecodeToWidth(imageStream, 64));
        }

        public static async Task<Stream> LoadPotraitBitmapAsync(string characterid)
        {
            var data = await s_httpClient.GetByteArrayAsync(ResolveUrl(characterid));
            return new MemoryStream(data);
        }

        public void CopyCharacterFiles()
        {
            //Copy character files
            string dateNow = DateTime.Now.ToString("h-mm-dd-MM-yy");
            string settingsBackup = $"settings_Backup-{dateNow}";
            if (backupPath != null)
            {
                DirectoryInfo? backUpDirectory = Directory.CreateDirectory(Path.Combine(path1: backupPath,
                                                                                        path2: settingsBackup));
                if (ToSelectedItems != null)
                    foreach (var (item, character) in from Character? item in ToSelectedItems
                                                      let character = FromSelectedItem
                                                      select (item, character))
                    {
                        if (character.CharacterName != item.CharacterName)
                        {
                            string? fileName = Path.GetFileName(item.CharacterFilePath);
                            string? backupFilePath = System.IO.Path.Combine(backUpDirectory.FullName, fileName);
                            try
                            {
                                if (item.CharacterFilePath != null)
                                    File.Copy(sourceFileName: item.CharacterFilePath,
                                              backupFilePath,
                                              true);
                            }
                            catch (IOException copyError)
                            {
                                Debug.WriteLine(copyError.Message);
                            }
                            try
                            {
                                if (character.CharacterFilePath != null && item.CharacterFilePath != null)
                                    File.Copy(character.CharacterFilePath, item.CharacterFilePath, true);
                            }
                            catch (IOException copyError)
                            {
                                Debug.WriteLine(copyError.Message);
                            }
                            Debug.WriteLine($"Name:{character.CharacterName} ID:{character.CharacterId} copied to Name:{item.CharacterName} ID:{item.CharacterId}");
                            Debug.WriteLine($"From {character.CharacterFilePath}");
                            Debug.WriteLine($"To {item.CharacterFilePath}");
                            Debug.WriteLine("------------------------------------------------------------------");
                        }
                        else
                        {
                            Debug.WriteLine($"Passed {item.CharacterName}");
                            Debug.WriteLine("------------------------------------------------------------------");
                            continue;
                        }
                    }
            }
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
                    string tranqFolder = Path.Combine(eveFolder,
                                                      eveFolderItem);
                    string[] tranqFolderList = Directory.GetDirectories(tranqFolder);
                    backupPath = tranqFolder;
                    foreach (string tranqFolderItem in tranqFolderList)
                    {
                        //Looks for settings_Default folder
                        if (tranqFolderItem.EndsWith("Default"))
                        {
                            string settingsPath = Path.Combine(tranqFolder,
                                                               tranqFolderItem);
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

        private static string? ResolveUrl(string characterId)
        {
            dynamic? json = JsonHandler($"https://esi.evetech.net/latest/characters/{characterId}/portrait/?datasource=tranquility");
            string? purl = json.px64x64;
            return purl;
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

        private async void GetCharacter(string characterid, string characterfilepath)
        {
            dynamic? json = JsonHandler($"https://esi.evetech.net/latest/characters/{characterid}/?datasource=tranquility");
            string? characterName = json.name;
            Character character = new()
            {
                CharacterName = characterName,
                CharacterId = characterid,
                CharacterFilePath = characterfilepath,
                CharacterPotrait = await LoadPotrait(characterid)
            };
            AddCharacter(character);
        }
    }
}