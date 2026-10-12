using System.Drawing;
using System.Drawing.Drawing2D;

internal static class DemoSlides
{
    internal static void Draw(Bitmap image,int index)
    {
        using(var g=Graphics.FromImage(image)) {
            g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float w=image.Width,h=image.Height,s=w/1600f;
            g.Clear(Color.FromArgb(249,251,254));
            using(var blue=new SolidBrush(Color.FromArgb(7,126,240))) g.FillRectangle(blue,0,0,w,20*s);
            using(var ink=new SolidBrush(Color.FromArgb(18,38,65))) using(var blue=new SolidBrush(Color.FromArgb(7,126,240))) using(var muted=new SolidBrush(Color.FromArgb(101,122,149)))
            using(var title=new Font("Segoe UI",82*s,FontStyle.Bold,GraphicsUnit.Pixel)) using(var small=new Font("Segoe UI",34*s,FontStyle.Regular,GraphicsUnit.Pixel)) using(var subtitle=new Font("Segoe UI",44*s,FontStyle.Regular,GraphicsUnit.Pixel)) using(var body=new Font("Segoe UI",54*s,FontStyle.Regular,GraphicsUnit.Pixel)) using(var heading=new Font("Segoe UI",40*s,FontStyle.Bold,GraphicsUnit.Pixel)) {
                g.DrawString("PROJECT BASICS  /  "+(index+1).ToString("00"),small,blue,85*s,65*s);
                string[] titles={"From idea to a clear plan","Three steps. One outcome.","Notes that stay useful","Review brings clarity"};
                g.DrawString(titles[index],title,ink,new RectangleF(85*s,125*s,w-170*s,200*s));
                string[] subtitles={"Start with a goal. Make progress visible.","A simple workflow for small projects.","Connect the words, examples and next steps.","Compare results before choosing the next step."};
                g.DrawString(subtitles[index],subtitle,muted,new RectangleF(85*s,300*s,w-170*s,85*s));
                bool vertical=h>w*1.2f;
                string[] labels=index==0?new[]{"01  DEFINE","02  BUILD","03  REVIEW"}:index==1?new[]{"QUESTION","EXAMPLE","RESULT"}:index==2?new[]{"KEY IDEA","EXAMPLE","NEXT STEP"}:new[]{"Clarity","Progress","Confidence"};
                string[] texts=index==0?new[]{"Clear goal\nUseful scope","Small steps\nTest an idea","Check results\nChoose next"}:index==1?new[]{"What changes?\nWho benefits?","Try one case\nCheck it","Keep the idea\nExplain it"}:index==2?new[]{"Use your own words","Keep a short example","Choose one action"}:new[]{"Clear goal\nShared view","Small steps\nFeedback","Useful idea\nTested result"};
                float gap=26*s,cardW=vertical?w-170*s:(w-170*s-gap*2)/3;
                float cardH=vertical?310*s:300*s;
                for(int i=0;i<3;i++) {
                    float x=85*s+(vertical?0:i*(cardW+gap)),y=425*s+(vertical?i*(cardH+gap):0);
                    using(var fill=new SolidBrush(i==1?Color.FromArgb(226,241,255):Color.White)) g.FillRectangle(fill,x,y,cardW,cardH);
                    using(var line=new Pen(Color.FromArgb(208,224,243),2*s)) g.DrawRectangle(line,x,y,cardW,cardH);
                    g.DrawString(labels[i],heading,blue,x+28*s,y+26*s);
                    g.DrawString(texts[i],body,ink,new RectangleF(x+28*s,y+100*s,cardW-50*s,190*s));
                }
                if(index==3 && !vertical) {
                    for(int i=0;i<3;i++) { using(var bar=new SolidBrush(Color.FromArgb(7,126,240))) g.FillRectangle(bar,85*s+i*(cardW+gap),765*s,cardW*(.45f+.22f*i),24*s); }
                }
                g.DrawString("Fictional teaching example  ·  SaveLecture",small,muted,85*s,h-65*s);
                g.DrawString((index+1).ToString("00"),small,blue,w-120*s,h-65*s);
            }
        }
    }
}
