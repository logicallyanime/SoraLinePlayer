using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using NAudio.Vorbis;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TitsPlay.Models;
using Xceed.Words.NET;



namespace TitsPlay.Views;

public partial class MainWindow : Window
{
    private static readonly Regex rxPattern = MyRegex();

    private int cursor = 0;
    private List<Line>? queue;
    private List<Line>? queueFull;
    private bool Exists => cursor >= 0 && cursor < queue?.Count;
    private Line? CurLine => Exists ? queue?[cursor] : null;

    private bool oggFolderSet = true;
    private bool docxFileSet = false;
    private Uri? folderSelected;

    private TextBlock? LineTextObj => this.FindControl<TextBlock>("LineText");
    private TextBlock? CurrentSpeakerTextObj => this.FindControl<TextBlock>("CurrentSpeakerText");
    private TextBlock? CurrentFileTextObj => this.FindControl<TextBlock>("CurrentFileText");
    private ComboBox? VoiceCombo => this.FindControl<ComboBox>("VoiceComboBox");
    private Button PrevBtn => this.FindControl<Button>("PreviousButton") ?? new Button();
    private Button NextBtn => this.FindControl<Button>("NextButton") ?? new Button();

    private static readonly ObservableCollection<string> defaultBox = new(["-------"]);
    private ObservableCollection<string> SpkrItems = defaultBox;
    private string? SelSpkr
    {
        get
        {
            string? selectedItem;
            if (VoiceCombo != null) { 
                selectedItem = VoiceCombo.SelectedItem!.ToString(); 
            return selectedItem;
            }return null;
        }
    }

    private bool IsSelectedSpkr
    {
        get
        {
            /*if (SelSpkr == null) return false;*/
            if (NotNull(SelSpkr))
                return !SelSpkr.Equals(SpkrItems[0]);
            return false;
        }
    }

    private string CurSpkr => Exists ? queue![cursor].Speaker : "";

    private WaveOutEvent? outputDevice;
    private VorbisWaveReader? audioFile;


    private static bool NotNull([NotNullWhen(true)] object? obj) => obj is not null;
    private static bool IsNull([NotNullWhen(false)] object? obj) => obj is null;


    public MainWindow()
    {
        InitializeComponent();


        var docBtn = this.FindControl<Button>("LoadDocxButton");
        var selFolderBtn = this.FindControl<Button>("SelectFolderButton");
        var playBtn = this.FindControl<Button>("PlayButton");
        var comboBox = this.FindControl<ComboBox>("VoiceComboBox");

        if(NotNull(docBtn) && NotNull(selFolderBtn) && NotNull(PrevBtn) && NotNull(playBtn) && NotNull(NextBtn) && NotNull(comboBox))
        {
            docBtn.Click += LoadDocx;
            selFolderBtn!.Click += SelectFolder;
            PrevBtn.Click += Previous;
            playBtn.Click += Play;
            NextBtn.Click += Next;
            comboBox.ItemsSource = SpkrItems;
            comboBox.SelectedItem = SpkrItems[0];
            comboBox.IsEnabled = true;
            comboBox.SelectionChanged += onCBSel!;
        }



        // var docButton = this.FindControl<Button>("LoadDocxButton").Click += LoadDocx;
        // this.FindControl<Button>("").Click += ;
        // this.FindControl<Button>("").Click += ;
        // this.FindControl<Button>("").Click += ;
        // this.FindControl<Button>("").Click += ;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void onCBSel(object sender, EventArgs e)
    {
        if( IsSelectedSpkr ) cursor = 0;
        GetNewQueue();

        UpdateText();

    }

    private async void SelectFolder(object? sender, RoutedEventArgs e){

        var results = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions(){
            Title = "Select Ogg Folder",
            AllowMultiple = false
        });
        if (results != null && results.Count > 0){
            folderSelected =  results.First().Path;
            oggFolderSet = true;
            if(docxFileSet) UpdateText();
        }
    }
    private void PopulateSpkrs()
    {
        if (!NotNull(queue)) return;
        var newSpkrList = SpkrItems; 
        queue.ForEach((Line line) =>
        {
            var spkr = line.Speaker;
            if (!newSpkrList.Contains(spkr))
            {
                newSpkrList.Add(spkr);
            }
        });

        VoiceCombo!.ItemsSource = newSpkrList;
    }

    private void GetNewQueue()
    {
        if (IsNull(queueFull)) return;
        if(CurLine == null) return;
        if (!IsSelectedSpkr)
        {
            cursor = queueFull.IndexOf(CurLine);
            queue = queueFull;
            return;
        }

        queue = new List<Line>();
        queueFull.ForEach((Line line) =>
        {
            var spkr = line.Speaker;

            if (spkr.Equals(SelSpkr))
            {
                queue.Add(line);
            }
        });
        

    }

    private async void LoadDocx(object? sender, RoutedEventArgs e){

        var results = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions(){
            Title = "Select Docx File",
            AllowMultiple = false,
            FileTypeFilter = new[] {Docx}

        });
        if (results != null && results.Count > 0){
            docxFileSet = true;
            var docxSelected = results[0].Path.LocalPath;
            queueFull = ParseDocx(docxSelected);
            queue = queueFull;
            if(oggFolderSet) UpdateText();
            PopulateSpkrs();

        }


    }


    public static FilePickerFileType Docx {get;} = new("Word Document")
    {
        Patterns = new[] {"*.docx"}
    }; 

    private static List<Line> ParseDocx(string path){

        var matches = new List<Line>();
        using (var doc = DocX.Load(path))
        {
            foreach (var paragraph in doc.Paragraphs)
            {
                var match = rxPattern.Match(paragraph.Text);

                if (!match.Success) continue;

                var groups = match.Groups.Cast<Group>().Select(x => x.Value).ToArray();


                if (!string.IsNullOrEmpty(groups[1]))
                    matches.Add(new Line(groups[1], groups[2], groups[3]));
                else{

                    if(groups[3].StartsWith("ch"))
                        matches.Last().AddLine(groups[2], groups[3]);
                    else
                        throw new System.Exception("Failed to parse line: " + paragraph.Text);
                }
            }
        }
        return matches;
    }

    private void UpdateText(){

        if(IsNull(CurLine )|| IsNull(queue) || queue.Count == 0) return;
        var spkr = CurLine.Speaker;
        var lineObj = CurLine.Peek();
        if(CurrentFileTextObj == null || CurrentSpeakerTextObj == null || LineTextObj == null) return;
        CurrentFileTextObj.Text = lineObj.Id;
        CurrentSpeakerTextObj.Text = spkr;
        LineTextObj.Text = lineObj.Line;
    }


    private void Previous(object? sender, RoutedEventArgs e){
        if(IsNull(CurLine) || IsNull(queue) || queue.Count == 0 ) return;
        if (CurLine.HasPrev()) CurLine.Prev();
        else
        {
            if (cursor == 0) return;
            cursor--;
        }

        UpdateText();
        Play(sender, e);
    }
    private async void Next(object? sender, RoutedEventArgs e){
        if(IsNull(CurLine) || IsNull(queue) || queue.Count == cursor)  return;
        if (CurLine.HasNext())  CurLine.Next();
        else
        {
            if (cursor >= queue.Count - 1) return;       // Return if No mo Lines
            cursor++;
        }
        UpdateText();
        await Task.Delay(500);
        Play(sender, e);
    }

    private async void Play(object? sender, RoutedEventArgs e){
        StopPlayback();
        if (!oggFolderSet || !docxFileSet || IsNull(CurLine) || IsNull(folderSelected)) return;
        var curVoice = CurLine.Peek().Id;
        var path = Path.Combine(folderSelected.LocalPath, curVoice + ".ogg");
        if (File.Exists(path))
        {
            using (audioFile = new VorbisWaveReader(path))
            using (outputDevice = new WaveOutEvent())
            {
                outputDevice.Init(audioFile);
                outputDevice.Play();

                var tcs = new TaskCompletionSource<object?>();

                outputDevice.PlaybackStopped += (s, a) => tcs.SetResult(null);
                await tcs.Task;
            }
        } else
        {
            Debug.WriteLine(path + " cannot be found");
        }
    }

    private void StopPlayback()
    {
        if (outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing)
        {
            outputDevice.Stop();
        }
        audioFile?.Dispose();
        outputDevice?.Dispose();
        audioFile = null;
        outputDevice = null;
    }

    [GeneratedRegex(@"^(?:(.*): ){0,1}(.*)\((?<=\()(ch.*)(?=\))")]
    private static partial Regex MyRegex();
}
