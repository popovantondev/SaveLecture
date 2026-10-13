using System;
using System.Drawing;
using System.IO;
using System.Linq;
using SaveLecture;

internal static class AcceptanceTests
{
    private static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
    public static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "SaveLecture-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using(var clearBoard=new Bitmap(240,135)) {
            using(var g=Graphics.FromImage(clearBoard)) {
                g.Clear(Color.FromArgb(18,18,18));
                for(int y=12;y<125;y+=6) for(int x=10;x<230;x+=13)
                    g.FillRectangle(Brushes.LightGreen,x,y,7,2);
            }
            using(var popupBoard=(Bitmap)clearBoard.Clone()) {
                using(var g=Graphics.FromImage(popupBoard)) {
                    g.FillRectangle(Brushes.WhiteSmoke,135,18,65,28);
                    g.FillRectangle(Brushes.DarkSlateBlue,139,37,57,6);
                    g.DrawLine(Pens.Gray,141,25,190,25);
                    g.DrawLine(Pens.Gray,141,30,185,30);
                }
                var clearFeature=SlideArchive.Feature(clearBoard); var popupFeature=SlideArchive.Feature(popupBoard);
                Check(FrameChanges.Occludes(clearFeature,popupFeature) && !FrameChanges.Occludes(popupFeature,clearFeature),"dialog covering drawing is directional, its disappearance reveals content");
                Check(!LiveSlideFilter.Duplicate(popupFeature,clearFeature),"clean drawing is not discarded as duplicate of an obscured capture");
                Check(SlideSelection.IsMoreComplete(popupFeature,clearFeature) && !SlideSelection.IsMoreComplete(clearFeature,popupFeature),"clean drawing is preferred over dialog despite fewer bright pixels");
                string popupRoot=Path.Combine(root,"popup-first"); var popupTime=new DateTime(2026,10,13,12,0,0);
                var popupArchive=new SlideArchive(popupRoot) { StabilitySeconds=3 };
                popupArchive.Observe(popupBoard,popupTime);
                string obscured=popupArchive.Observe(popupBoard,popupTime.AddSeconds(3));
                byte[] obscuredBytes=File.ReadAllBytes(obscured);
                Check(popupArchive.Observe(clearBoard,popupTime.AddSeconds(4))==null,"revealed slide waits for stability instead of keeping saved candidate");
                string clearPath=popupArchive.Observe(clearBoard,popupTime.AddSeconds(7));
                var popupLocations=new SlideRelocations(Path.Combine(popupRoot,"13.10.2026"));
                Check(clearPath!=null && popupArchive.Representative(obscured)==clearPath && popupArchive.Duplicates==1,"gallery replaces dialog capture with clean stable slide and counts replacement");
                Check(File.ReadAllBytes(popupLocations.Resolve(obscured)).SequenceEqual(obscuredBytes),"obscured original is preserved byte-for-byte in Removed");
                var restoredPopup=new SlideArchive(popupRoot) { StabilitySeconds=0 };
                Check(restoredPopup.Observe(popupBoard,popupTime.AddSeconds(9))==null && restoredPopup.LastImagePath==clearPath,"restart never promotes old dialog over clean representative");
                Check(!FrameChanges.Occludes(differentBoard(clearFeature),popupFeature),"unrelated drawing is not treated as dialog disappearance");
                using(var busyPopup=(Bitmap)popupBoard.Clone()) {
                    using(var g=Graphics.FromImage(busyPopup)) {
                        g.FillRectangle(Brushes.LightGray,210,8,24,119);
                        for(int y=12;y<120;y+=10) g.FillRectangle(Brushes.DimGray,215,y,15,6);
                        for(int y=22;y<34;y+=3) g.DrawLine(Pens.Gray,140,y,191,y);
                    }
                    Check(FrameChanges.Occludes(clearFeature,SlideArchive.Feature(busyPopup)),"dialog with dense text and simultaneously closing side palette reveals the same drawing");
                }
                var reuseSelection=new SlideSelection(); reuseSelection.Add("clean.jpg",clearFeature); reuseSelection.Add("popup.jpg",popupFeature); reuseSelection.Add("clean.jpg",clearFeature);
                Check(reuseSelection.Representative("popup.jpg")=="clean.jpg" && reuseSelection.EarlierVersions("clean.jpg").Length==1,"reusing already saved clean frame does not duplicate gallery groups");
            }
        }
        using(var waitingFirst=TrayStates.Render(5,16)) using(var waitingNext=TrayStates.Render(6,16)) {
            int purple=0, different=0;
            for(int y=0;y<16;y++) for(int x=0;x<16;x++) {
                var pixel=waitingFirst.GetPixel(x,y);
                if(pixel.A>100 && pixel.B>pixel.R && pixel.R>pixel.G) purple++;
                if(pixel.ToArgb()!=waitingNext.GetPixel(x,y).ToArgb()) different++;
            }
            Check(purple>10 && different>10,"waiting tray ring is purple and its animation frames differ at actual 16px size");
        }
        var drawingBoard=new byte[240*135];
        for(int y=0;y<5;y++) for(int x=0;x<240;x++) drawingBoard[y*240+x]=180;
        for(int y=30;y<35;y++) for(int x=50;x<80;x++) drawingBoard[y*240+x]=200;
        var drawingFinished=(byte[])drawingBoard.Clone();
        for(int y=70;y<75;y++) for(int x=100;x<130;x++) drawingFinished[y*240+x]=200;
        for(int y=0;y<5;y++) for(int x=40;x<50;x++) drawingFinished[y*240+x]=0;
        Check(SlideSelection.IsMoreComplete(drawingBoard,drawingFinished),"drawing additions consolidate despite toolbar changes");
        var drawingErased=(byte[])drawingFinished.Clone();
        for(int y=30;y<35;y++) for(int x=50;x<80;x++) drawingErased[y*240+x]=0;
        Check(!SlideSelection.IsMoreComplete(drawingFinished,drawingErased),"erasing drawing content keeps a separate state");
        var drawingOther=(byte[])drawingBoard.Clone();
        for(int y=30;y<35;y++) for(int x=50;x<80;x++) drawingOther[y*240+x]=0;
        for(int y=60;y<65;y++) for(int x=160;x<190;x++) drawingOther[y*240+x]=200;
        Check(!SlideSelection.IsMoreComplete(drawingBoard,drawingOther),"a different board is not merged by its shared background");
        var drawingSelection=new SlideSelection(); drawingSelection.Add("first.png",drawingBoard); drawingSelection.Add("final.png",drawingFinished);
        Check(drawingSelection.Representative("first.png")=="final.png" && drawingSelection.EarlierVersions("final.png").Length==1,"earlier drawing resolves to latest representative and remains recoverable");
        drawingSelection.Add("first.png",drawingFinished);
        Check(drawingSelection.Representative("first.png")=="final.png" && !drawingSelection.EarlierVersions("final.png").Contains("final.png"),"reobserving predecessor cannot resurrect a moved path or create a self reference");
        string moveRoot=Path.Combine(root,"progressive-archive");
        var moveTime=new DateTime(2026,10,12,12,0,0);
        using(var moveDrawing=new Bitmap(1000,600)) {
            using(var g=Graphics.FromImage(moveDrawing)) { g.Clear(Color.White); g.FillRectangle(Brushes.Black,100,100,300,80); }
            using(var originalDrawing=(Bitmap)moveDrawing.Clone()) {
                var moveArchive=new SlideArchive(moveRoot) { StabilitySeconds=0 };
                string firstImage=moveArchive.Observe(moveDrawing,moveTime);
                byte[] originalImageBytes=File.ReadAllBytes(firstImage);
                string moveDay=Path.Combine(moveRoot,"12.10.2026"), removed=Path.Combine(moveDay,"Removed");
                Directory.CreateDirectory(removed);
                string occupied=Path.Combine(removed,Path.GetFileName(firstImage)); File.WriteAllText(occupied,"Preserve existing file");
                using(var g=Graphics.FromImage(moveDrawing)) g.FillRectangle(Brushes.Black,500,300,25,15);
                string finalImage=moveArchive.Observe(moveDrawing,moveTime.AddSeconds(5));
                var movedLocations=new SlideRelocations(moveDay);
                string relocated=movedLocations.Resolve(firstImage);
                Check(finalImage!=null && File.Exists(finalImage) && !File.Exists(firstImage) && Path.GetDirectoryName(relocated)==removed,"fuller slide stays in scr while its earlier version moves to Removed");
                Check(File.ReadAllBytes(relocated).SequenceEqual(originalImageBytes) && File.ReadAllText(occupied)=="Preserve existing file","relocation preserves exact image bytes and does not overwrite a name collision");
                Check(moveArchive.Duplicates==1 && moveArchive.Observe(moveDrawing,moveTime.AddSeconds(6))==null && moveArchive.Duplicates==1,"a replaced slide counts as one duplicate without repeated counting");
                string moveBest=File.ReadAllText(Path.Combine(moveDay,"_service","slides_best.json"));
                Check(moveBest.Contains("Removed/") && moveBest.Contains(Path.GetFileName(finalImage)),"best-slide index uses the actual relocated path");
                var restartMoves=new SlideArchive(moveRoot) { StabilitySeconds=0 };
                Check(restartMoves.Observe(originalDrawing,moveTime.AddSeconds(10))==null && restartMoves.LastImagePath==finalImage && restartMoves.Duplicates==1,"restart deduplicates an earlier moved frame and displays its final version");
                Check(restartMoves.ResolveImagePath(firstImage)==finalImage && File.Exists(restartMoves.ResolveImagePath(firstImage)),"gallery resolves original timeline path to a readable current representative");
                File.AppendAllText(Path.Combine(moveDay,"_service","slides_relocations.jsonl"),"{\"source\":");
                var afterTornRecord=new SlideRelocations(moveDay);
                Check(afterTornRecord.Resolve(firstImage)==relocated,"a torn final relocation record does not hide earlier completed moves");
                string extra=Path.Combine(moveDay,"scr","extra.jpg"); File.WriteAllBytes(extra,new byte[]{1,2,3});
                Check(afterTornRecord.MoveEarlier(extra) && new SlideRelocations(moveDay).Resolve(extra)!=extra,"a subsequent durable relocation is readable after a torn journal tail");
                string blocked=Path.Combine(moveDay,"scr","blocked.jpg"); File.WriteAllBytes(blocked,new byte[]{4,5,6});
                bool blockedMove=false;
                using(var locked=new FileStream(blocked,FileMode.Open,FileAccess.Read,FileShare.None)) {
                    try { new SlideRelocations(moveDay).MoveEarlier(blocked); } catch(IOException) { blockedMove=true; }
                }
                Check(blockedMove && File.ReadAllBytes(blocked).SequenceEqual(new byte[]{4,5,6}) && new SlideRelocations(moveDay).Resolve(blocked)==blocked,"a blocked relocation retains the original and resolves its unfinished intent safely");
                Check(new SlideRelocations(moveDay).MoveEarlier(blocked) && File.Exists(new SlideRelocations(moveDay).Resolve(blocked)),"a blocked relocation can be retried without losing the image");
                string outside=Path.Combine(root,"outside.jpg"); File.WriteAllText(outside,"Outside recording"); bool outsideRefused=false;
                try { new SlideRelocations(moveDay).MoveEarlier(outside); } catch(IOException) { outsideRefused=true; }
                Check(outsideRefused && File.ReadAllText(outside)=="Outside recording","relocation rejects an image outside this day's scr directory");
            }
        }
        string project = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var matcher = new NameMatcher(new[]{"Demo A","Demo B","Speaker A","Speaker B","Demo-A","Demo-B","Demo C","Demo D","Demo E"});
        foreach (string text in new[] { "Demo A", "Demo B, bitte", "SPEAKER A", "Herr Speaker B", "Demo-A", "Demo-B", "Demo C", "Demo D", "Demo E" })
            Check(matcher.Count(text) > 0, "mention variant: " + text);
        Check(matcher.Count("Die Tabelle wird ausgefüllt") == 0, "unrelated German sentence does not trigger");
        Check(new NameMatcher(new string[0]).Count("Demo A")==0,"empty alias list disables mentions safely");
        var sample=new byte[240*135]; var shifted=new byte[sample.Length]; var random=new Random(42); random.NextBytes(sample);
        for(int y=0;y<135;y++) for(int x=6;x<240;x++) shifted[y*240+x]=sample[y*240+x-6];
        LiveSlideFilter.MaxShift=0; double uncompensated=LiveSlideFilter.Score(sample,shifted);
        LiveSlideFilter.MaxShift=6; double compensated=LiveSlideFilter.Score(sample,shifted);
        Check(compensated>.99 && uncompensated<.9,"shift threshold affects real frame comparison"); LiveSlideFilter.MaxShift=12;
        var before = new DateTime(2026, 10, 10, 23, 59, 59);
        var after = before.AddSeconds(2);
        var decider = new NameAlertDecider();
        Check(decider.Accept("row", 1, before) && !decider.Accept("row", 1, before.AddMilliseconds(150)) && decider.Accept("another", 1, before.AddMilliseconds(300)), "growing caption suppressed; a new utterance alerts immediately");
        Check(decider.Accept("row", 1, before.AddSeconds(6)), "same reused control alerts after a new utterance gap");
        string oldText;
        string resumeRoot = Path.Combine(root, "resume");
        string resumedPath;
        using (var first = new ArchiveStore(resumeRoot, true))
        {
            first.AppendRaw("pending", "Durable unfinished sentence", "Speaker", DateTime.Now);
            resumedPath = first.TranscriptPath;
        }
        using (var resumed = new ArchiveStore(resumeRoot, true))
        {
            resumed.AppendRaw("next", "After reboot", "Speaker", DateTime.Now);
            resumed.AppendText("After reboot", "Speaker", DateTime.Now);
            Check(resumed.TranscriptPath == resumedPath, "abrupt restart resumes the same lecture TXT");
            resumed.MarkClean();
        }
        Check(File.ReadAllText(resumedPath).Contains("Durable unfinished sentence") && File.ReadAllText(resumedPath).Contains("After reboot"), "pending raw sentence recovered into the same TXT without truncation");
        using (var next = new ArchiveStore(resumeRoot, true))
        {
            next.AppendRaw("new", "New lecture after Stop", "", DateTime.Now);
            Check(next.TranscriptPath != resumedPath, "explicit Stop starts a different lecture");
            next.MarkClean();
        }
        using (var store = new ArchiveStore(root))
        {
            store.AppendRaw("row1", "Demo test", "Speaker", before);
            store.AppendText("Demo test", "Speaker", before);
            oldText = store.TranscriptPath;
            store.AppendRaw("row2", "next day", "Speaker", after);
            store.AppendText("next day", "Speaker", after);
            store.AppendText("delayed old day", "Speaker", before);
        }
        Check(File.ReadAllText(oldText).Contains("Demo test") && File.ReadAllText(oldText).Contains("delayed old day") && !File.ReadAllText(oldText).Contains("next day"), "midnight routes observations to their own date");
        string original = File.ReadAllText(oldText);
        using (var store = new ArchiveStore(root)) { store.AppendRaw("new", "restart", "", before); store.AppendText("restart", "", before); }
        Check(File.ReadAllText(oldText) == original && Directory.GetFiles(Path.GetDirectoryName(oldText), "*.txt").Length == 2, "restart creates new session and preserves original bytes");
        string journal = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(oldText), "_service"), "*.jsonl").First(p => File.ReadAllText(p).Contains("Demo test"));
        File.AppendAllText(journal, "{\"text\":");
        string recovered = Path.Combine(root, "Recovered.txt");
        Check(ArchiveStore.Recover(journal, recovered) == 1 && File.ReadAllText(recovered).Contains("Demo test"), "recover durable raw caption despite torn final record");
        bool refused = false;
        try { ArchiveStore.Recover(journal, recovered); } catch (IOException) { refused = true; }
        Check(refused, "recovery refuses to overwrite output");
        using (var slide = new Bitmap(1000, 600))
        {
            using (var g = Graphics.FromImage(slide)) { g.Clear(Color.White); g.FillRectangle(Brushes.Black, 100, 100, 300, 80); }
            var slides = new SlideArchive(root);
            slides.StabilitySeconds = 13;
            byte[] initialFeature = SlideArchive.Feature(slide);
            Check(slides.Observe(slide, before) == null && slides.Observe(slide, before.AddSeconds(12)) == null, "no slide before 13 continuous seconds including date rollover");
            string saved = slides.Observe(slide, before.AddSeconds(26));
            Check(saved != null && File.Exists(saved), "stable JPG committed after 13 seconds");
            var restarted = new SlideArchive(root);
            restarted.StabilitySeconds = 13;
            restarted.Observe(slide, before.AddSeconds(27));
            Check(restarted.Observe(slide, before.AddSeconds(41)) == null, "day dedup survives restart");
            using (var g = Graphics.FromImage(slide)) g.FillRectangle(Brushes.Black, 500, 300, 25, 15);
            Check(SlideSelection.IsMoreComplete(initialFeature, SlideArchive.Feature(slide)), "added table content is recognized as a fuller version");
            restarted.Observe(slide, before.AddSeconds(42));
            Check(restarted.Observe(slide, before.AddSeconds(56)) != null, "small distinct slide change is retained");
            string best = File.ReadAllText(Path.Combine(root, "11.10.2026", "_service", "slides_best.json"));
            Check(best.Contains("earlier_versions") && best.Contains(Path.GetFileName(saved)), "best-frame index links preserved earlier version");
            restarted.End(before.AddSeconds(57), "test_end");
            string timeline = File.ReadAllText(Path.Combine(root, "11.10.2026", "_service", "slides.jsonl"));
            Check(timeline.Contains("slide_start") && timeline.Contains("slide_end") && timeline.Contains("from_session_seconds"), "slide timeline links image, start, end and session clock");
        }
        string soakText;
        byte[] board = new byte[240*135];
        for (int i=0;i<board.Length;i++) board[i]=245;
        for (int y=30;y<60;y++) for (int x=20;x<100;x++) board[y*240+x]=20;
        byte[] mouseBefore=(byte[])board.Clone(), mouseAfter=(byte[])board.Clone();
        for(int y=80;y<84;y++) { mouseBefore[y*240+120]=20; mouseAfter[y*240+160]=20; }
        Check(FrameChanges.Transient(mouseBefore,mouseAfter), "moving cursor does not restart screenshot timer");
        byte[] verticalMouse=(byte[])board.Clone();
        for(int y=110;y<114;y++) verticalMouse[y*240+120]=20;
        Check(FrameChanges.Transient(mouseBefore,verticalMouse),"vertical cursor motion is ignored too");
        byte[] textAdded=(byte[])board.Clone();
        for(int y=90;y<94;y++) for(int x=130;x<136;x++) textAdded[y*240+x]=20;
        Check(!FrameChanges.Transient(board,textAdded), "new small table text is not suppressed as mouse noise");
        byte[] menu=(byte[])board.Clone();
        for(int y=70;y<95;y++) for(int x=140;x<180;x++) menu[y*240+x]=100;
        Check(FrameChanges.Transient(board,menu), "compact context overlay is ignored");
        using(var initial = new Bitmap(240,135)) using(var overlay = new Bitmap(240,135))
        {
            for(int y=0;y<135;y++) for(int x=0;x<240;x++)
            {
                int value=board[y*240+x]; initial.SetPixel(x,y,Color.FromArgb(value,value,value));
                value=menu[y*240+x]; overlay.SetPixel(x,y,Color.FromArgb(value,value,value));
            }
            var quick = new SlideArchive(Path.Combine(root,"six-seconds"));
            quick.StabilitySeconds = 6;
            quick.Observe(initial,before.Date);
            quick.Observe(overlay,before.Date.AddSeconds(3));
            string clean = quick.Observe(overlay,before.Date.AddSeconds(6));
            Check(clean != null, "default six-second wait is not reset by context overlay");
            using(var picture = new Bitmap(clean)) Check(picture.GetPixel(160,80).R>200,"clean candidate saved instead of menu overlay");
            var immediate = new SlideArchive(Path.Combine(root,"zero-seconds")); immediate.StabilitySeconds=0;
            Check(immediate.Observe(initial,before.Date)!=null,"zero-second setting disables screenshot waiting");
        }
        using (var blackPlayer = new Bitmap(1000, 600))
        {
            using (var g = Graphics.FromImage(blackPlayer)) { g.Clear(Color.LightGray); g.FillRectangle(Brushes.Black, 100, 60, 800, 540); }
            var filter = new SlideArchive(Path.Combine(root, "black-player"));
            filter.Observe(blackPlayer, before.Date);
            Check(filter.Observe(blackPlayer, before.Date.AddSeconds(15)) == null, "empty black player is not archived as lecture material");
        }
        using (var store = new ArchiveStore(Path.Combine(root, "eight-hours")))
        {
            for (int i = 0; i < 9600; i++)
            {
                DateTime time = before.Date.AddHours(8).AddSeconds(i * 3);
                store.AppendRaw("row" + i, "Synthetic lecture " + i, "Speaker", time);
                store.AppendText("Synthetic lecture " + i, "Speaker", time);
            }
            soakText = store.TranscriptPath;
        }
        Check(File.ReadLines(soakText).Count() == 9600, "accelerated 8-hour synthetic stream retains all 9600 captions");
        Console.WriteLine("TEST_ARTIFACTS=" + root);
    }
    private static byte[] differentBoard(byte[] source) { var result=(byte[])source.Clone(); for(int i=0;i<result.Length;i++) result[i]=(byte)(255-result[i]); return result; }
}
