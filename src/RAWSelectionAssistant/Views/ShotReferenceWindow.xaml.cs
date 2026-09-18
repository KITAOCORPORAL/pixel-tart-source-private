using System.Windows;
using System.Windows.Input;

namespace RAWSelectionAssistant.Views;

public partial class ShotReferenceWindow : Window
{
    public ShotReferenceWindow(){InitializeComponent();PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};}
}
