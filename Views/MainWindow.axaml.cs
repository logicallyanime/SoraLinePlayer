using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NAudio.Wave;
using NAudio.Vorbis;
using Xceed.Words.NET;
using TitsPlay.Models;
using Avalonia.Platform.Storage;
using System;
using System.Threading.Tasks;



namespace TitsPlay.Views;

public partial class MainWindow : Window
{

    private static readonly Regex rxPattern = new Regex(@"^(?:(.*): ){0,1}(.*)\((?<=\()(ch.*)(?=\))");

    private int cursor = 0;
    private bool oggFolderSet = true;
    private bool docxFileSet = false;
    private List<Line> queue;
    private LineItem currLine;
    private Uri folderSelected;

    private TextBlock? LineTextObj => this.FindControl<TextBlock>("LineText");
    private TextBlock? CurrentSpeakerTextObj => this.FindControl<TextBlock>("CurrentSpeakerText");
    private TextBlock? CurrentFileTextObj => this.FindControl<TextBlock>("CurrentFileText");

    private WaveOutEvent outputDevice;
    private VorbisWaveReader audioFile;

    private bool isNull(object? obj) => obj == null;

    private bool areAnyNull(params object?[] objs) => objs.Any(x => x == null);

    public MainWindow()
    {
        InitializeComponent();


        var docBtn = this.FindControl<Button>("LoadDocxButton");
        var selFolderBtn = this.FindControl<Button>("SelectFolderButton");
        var prevBtn = this.FindControl<Button>("PreviousButton");
        var playBtn = this.FindControl<Button>("PlayButton");
        var nextBtn = this.FindControl<Button>("NextButton");

        if(!areAnyNull(docBtn, selFolderBtn, prevBtn, playBtn, nextBtn)){
            docBtn.Click += LoadDocx;
            selFolderBtn.Click += SelectFolder;
            prevBtn.Click += Previous;
            playBtn.Click += Play;
            nextBtn.Click += Next;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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

    private async void LoadDocx(object? sender, RoutedEventArgs e){

        var results = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions(){
            Title = "Select Docx File",
            AllowMultiple = false,
            FileTypeFilter = new[] {Docx}

        });
        if (results != null && results.Count > 0){
            docxFileSet = true;
            var docxSelected = results.First().Path.LocalPath;
            queue = ParseDocx(docxSelected);
            currLine = queue[cursor].Peek();
            if(oggFolderSet) UpdateText();
        }


    }

    public static FilePickerFileType Docx {get;} = new("Word Document"){
                Patterns = new[] {"*.docx"}
    };

    private List<Line> ParseDocx(string path){

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

        if(queue == null || queue.Count == 0) return;
        var spkr = queue[cursor].Speaker;
        var lineObj = currLine;
        if(CurrentFileTextObj == null || CurrentSpeakerTextObj == null || LineTextObj == null) return;
        CurrentFileTextObj.Text = lineObj.Id;
        CurrentSpeakerTextObj.Text = spkr;
        LineTextObj.Text = lineObj.Line;
    }


    private void Previous(object? sender, RoutedEventArgs e){
        if(queue == null || queue.Count == 0) return;
        if(cursor == 0) return;
        if(queue[cursor].HasPrev())
            currLine = queue[cursor].Prev();
        else{
            cursor--;
            currLine = queue[cursor].Peek();
        }
        UpdateText();
        Play(sender, e);
    }
    private async void Next(object? sender, RoutedEventArgs e){
        if(queue == null || queue.Count == cursor) return;
        if(queue[cursor].HasNext())
            currLine = queue[cursor].Next();
        else{
            cursor++;
            currLine = queue[cursor].Peek();
        }
        UpdateText();
        await Task.Delay(500);
        Play(sender, e);
    }

    private async void Play(object? sender, RoutedEventArgs e){
        StopPlayback();
        if (!oggFolderSet || !docxFileSet || currLine == null) return;
        var curVoice = currLine.Id;
        var path = Path.Combine(folderSelected.LocalPath, curVoice + ".ogg");
        using (audioFile = new VorbisWaveReader(path))
        using (outputDevice = new WaveOutEvent())
        {
            outputDevice.Init(audioFile);
            outputDevice.Play();

            var tcs = new TaskCompletionSource<object>();

            outputDevice.PlaybackStopped += (s, a) => tcs.SetResult(null);
            await tcs.Task;
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






}
