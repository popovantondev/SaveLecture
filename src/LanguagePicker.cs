using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;

namespace SaveLecture
{
    internal sealed class LanguagePicker : Border
    {
        readonly Button[] choices=new Button[3];
        readonly string[] languages={"ru","de","en"};
        internal string SelectedLanguage { get; private set; }
        internal LanguagePicker(string language,Action<string> changed)
        {
            Tag="UserContent"; Width=330; Padding=new Thickness(4);
            Background=new SolidColorBrush(Color.FromRgb(238,246,254));
            BorderBrush=new SolidColorBrush(Color.FromRgb(210,226,244)); BorderThickness=new Thickness(1); CornerRadius=new CornerRadius(12);
            var row=new UniformGrid { Columns=3 }; Child=row;
            var template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='Frame' CornerRadius='8' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Frame' Property='Background' Value='#DEEEFF'/></Trigger><MultiTrigger><MultiTrigger.Conditions><Condition Property='Tag' Value='Selected'/><Condition Property='IsMouseOver' Value='True'/></MultiTrigger.Conditions><Setter TargetName='Frame' Property='Background' Value='#006DE4'/></MultiTrigger></ControlTemplate.Triggers></ControlTemplate>");
            string[] names={"Русский","Deutsch","English"};
            for(int i=0;i<choices.Length;i++) {
                string selected=languages[i];
                var button=new Button { Name="Language"+selected,Content=names[i],Template=template,FontSize=16,FontWeight=FontWeights.SemiBold,Padding=new Thickness(10,8,10,8),Margin=new Thickness(1,0,1,0),FocusVisualStyle=null,Cursor=System.Windows.Input.Cursors.Hand };
                button.Click+=delegate { Select(selected); changed(selected); };
                choices[i]=button; row.Children.Add(button);
            }
            Select(language);
        }
        void Select(string language)
        {
            SelectedLanguage=language=="de"||language=="en"?language:"ru";
            for(int i=0;i<choices.Length;i++) {
                bool selected=languages[i]==SelectedLanguage; var button=choices[i];
                button.Tag=selected?"Selected":null;
                button.Background=selected?new SolidColorBrush(Color.FromRgb(7,131,255)):Brushes.Transparent;
                button.Foreground=selected?Brushes.White:new SolidColorBrush(Color.FromRgb(61,85,119));
            }
        }
    }
}
