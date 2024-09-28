<Page x:Class="LMC.Pages.DownloadingPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" 
      xmlns:d="http://schemas.microsoft.com/expression/blend/2008" 
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      xmlns:controls="clr-namespace:Wpf.Ui.Controls;assembly=Wpf.Ui"
      xmlns:local="clr-namespace:LMC"
      mc:Ignorable="d" 
      d:DesignHeight="468" d:DesignWidth="801" 
      Title="DownloadingPage">

    <Grid>
        <controls:Button x:Name="back" Margin="10,10,0,0" VerticalAlignment="Top" Height="50" Width="50" Click="back_Click">
            <controls:SymbolIcon Symbol="ArrowStepBack20" VerticalAlignment="Center" HorizontalAlignment="Center" Width="50" Height="50" FontSize="30"/>
        </controls:Button>
        <ui:ProgressRing x:Name="ring" IsIndeterminate="False" Height="130" Width="130" Margin="80,134,521,134" />
        <TextBlock x:Name="processing" Text="" Foreground="White" FontSize="20"  HorizontalAlignment="Left" Margin="337,100,0,0" VerticalAlignment="Top" Height="34" Width="382"/>
        <TextBlock x:Name="processing_progress" Text="" Foreground="White" FontSize="20"  HorizontalAlignment="Left" Margin="337,280,0,0" VerticalAlignment="Top" Height="34" Width="382"/>
    </Grid>
</Page>
