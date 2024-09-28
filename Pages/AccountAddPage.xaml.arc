<Page x:Class="LMC.Pages.AccountAddPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" 
      xmlns:d="http://schemas.microsoft.com/expression/blend/2008" 
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      xmlns:controls="clr-namespace:Wpf.Ui.Controls;assembly=Wpf.Ui"
      xmlns:local="clr-namespace:LMC"
      mc:Ignorable="d" 
      d:DesignHeight="468" d:DesignWidth="801"
      Title="AccountAddPage">

    <Grid>
        <controls:Button x:Name="back" Margin="10,10,0,0" VerticalAlignment="Top" Height="50" Width="50" Click="back_Click">
            <ui:SymbolIcon Symbol="ArrowStepBack20" VerticalAlignment="Center" HorizontalAlignment="Center" Width="50" Height="50" FontSize="30"/>
        </controls:Button>
        <Slider x:Name="lsl" HorizontalAlignment="Left" Margin="187,419,0,0" VerticalAlignment="Top" Width="378" RenderTransformOrigin="0.5,0.5" IsSnapToTickEnabled="True" Maximum="2" Minimum="1" Value="1" ValueChanged="Slider_ValueChanged" Ticks="1,2"/>
        <TabControl x:Name="ltc" Margin="91,-22,103,76" SelectedIndex="0">
            <TabItem Header="msa" Margin="816,71,-816,-71">
                <Grid>
                    <controls:TextBlock HorizontalAlignment="Left" Margin="35,33,0,0" TextWrapping="Wrap" VerticalAlignment="Top" Height="259" Width="531">
                        <ui:SymbolIcon Margin="220,30,0,30" FontSize="50" Symbol="Warning24" Width="54"/>
                        <TextBlock FontSize="20" TextWrapping="Wrap" Text="     在您点击“开始”按钮之后，将会打开浏览器进行登录操作，请在浏览器中完成登录，并在“是否同意Line Minecraft launCher使用您的数据”中点击“同意”，随后启动器会进行登录，期间或将造成启动器的微小卡顿。 请放心，LMC不会非法使用您的数据。" Height="137"/>
                    </controls:TextBlock>
                    <controls:Button x:Name="msa" Content="开始微软登录" Margin="196,308,0,0" VerticalAlignment="Top" Height="59" Width="174" Click="msa_Click"/>
                </Grid>
            </TabItem>
            <TabItem Header="offl" Margin="636,164,-636,-164">
                <Grid>
                    <controls:TextBox x:Name="ltb" Margin="132,129,53,0" TextWrapping="Wrap" Text="" VerticalAlignment="Top" TextChanged="TextBox_TextChanged"/>
                    <controls:TextBlock FontSize="22" HorizontalAlignment="Left" Margin="20,133,0,0" TextWrapping="Wrap" Text="游戏内ID" VerticalAlignment="Top" Height="40" Width="90"/>
                    <Button x:Name="lb" Content="确认"  Margin="415,248,0,0" VerticalAlignment="Top" Height="61" Width="139" Click="Button_Click"/>
                    <Label x:Name="stb" Foreground="Orange" HorizontalAlignment="Left" Margin="120,188,0,0" VerticalAlignment="Top" Height="40" Width="434" Content="请输入ID"/>

                </Grid>
            </TabItem>
        </TabControl>

    </Grid>
</Page>
