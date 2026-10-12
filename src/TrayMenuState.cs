using System.Windows.Controls;
using System.Windows.Media;
namespace SaveLecture
{
    internal static class TrayMenuState
    {
        internal static void Update(DesignWindow menu,bool stopped,bool paused,bool available,bool error,bool saving,bool? captions=null,bool? sharing=null)
        {
            bool textActive=captions??available, slideActive=sharing??available;
            menu.Find<TextBlock>("State").Text=stopped?"Остановлено":paused?"Пауза":error?"Ошибка":saving?"Сохранение":slideActive?(textActive?"Слайды и субтитры":"Слайды"):textActive?"Субтитры":"Ждём демонстрацию";
            bool waiting=!stopped && !paused && !error && !saving && !available;
            menu.Find<System.Windows.Shapes.Ellipse>("StateDot").Fill=new SolidColorBrush(stopped?Color.FromRgb(133,148,166):paused?Color.FromRgb(236,172,54):error?Color.FromRgb(235,83,99):waiting?Color.FromRgb(151,105,230):Color.FromRgb(45,203,130));
            menu.Find<Border>("StateBadge").Background=new SolidColorBrush(waiting?Color.FromRgb(242,235,255):paused?Color.FromRgb(255,245,219):stopped?Color.FromRgb(239,243,248):error?Color.FromRgb(255,235,238):Color.FromRgb(231,249,242));
            menu.Find<TextBlock>("State").Foreground=new SolidColorBrush(waiting?Color.FromRgb(117,72,190):paused?Color.FromRgb(150,102,20):stopped?Color.FromRgb(98,116,139):error?Color.FromRgb(190,49,66):Color.FromRgb(58,119,119));
            menu.Find<TextBlock>("RecordLabel").Text=stopped?"Старт записи":paused?"Продолжить":"Пауза";
            menu.Find<TextBlock>("RecordShortcut").Text=stopped?"Ctrl + R":"Ctrl + P";
            menu.Find<ContentControl>("RecordGlyph").Content=UiGlyph.Icon(stopped||paused?"play":"pause",new SolidColorBrush(Color.FromRgb(7,131,255)),20);
            menu.Find<Button>("Start").IsEnabled=true;
            menu.Find<Button>("Stop").IsEnabled=!stopped;
            UiText.Apply(menu.View);
        }
    }
}
