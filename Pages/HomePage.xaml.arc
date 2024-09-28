<Page x:Class="LMC.HomePage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" 
      xmlns:d="http://schemas.microsoft.com/expression/blend/2008" 
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      xmlns:controls="clr-namespace:Wpf.Ui.Controls;assembly=Wpf.Ui"
      xmlns:local="clr-namespace:LMC"
      mc:Ignorable="d" 
      d:DesignHeight="468" d:DesignWidth="801"
      Title="HomePage">

    <Grid>
        <TextBox x:Name="noticeTitle"  TextWrapping="Wrap" Text="公告" Margin="24,22,319,185" IsEnabled="False"/>

        <Label x:Name="notice" Content="公告" HorizontalAlignment="Left" Margin="37,60,0,0" VerticalAlignment="Top" Height="197" Width="424" FontSize="8"/>
        <controls:Button x:Name="launchButton" Content="启动游戏" Margin="640,363,0,0" VerticalAlignment="Top" Height="80" Width="135">
            <controls:Button.Icon>
                <ui:SymbolIcon Symbol="Open32"/>
            </controls:Button.Icon>
        </controls:Button>
        <TextBox x:Name="launchInfo"  TextWrapping="Wrap" Text=""  Margin="24,379,319,35" IsEnabled="False"/>
    </Grid>
</Page>
