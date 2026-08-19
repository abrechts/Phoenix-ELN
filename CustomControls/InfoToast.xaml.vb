Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Threading

Public Class InfoToast

    Private ReadOnly _timer As New DispatcherTimer


    Public Shared ReadOnly DisplayTimeProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(DisplayTime), GetType(TimeSpan), GetType(InfoToast),
            New PropertyMetadata(TimeSpan.FromMilliseconds(1200)))

    ''' <summary>
    ''' Sets or gets the DisplayTime property
    ''' </summary>
    ''' 
    Public Property DisplayTime As TimeSpan
        Get
            Return CType(GetValue(DisplayTimeProperty), TimeSpan)
        End Get
        Set(value As TimeSpan)
            SetValue(DisplayTimeProperty, value)
        End Set
    End Property


    Public Shared ReadOnly FadeTimeProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(FadeTime), GetType(TimeSpan), GetType(InfoToast),
            New PropertyMetadata(TimeSpan.FromMilliseconds(450)))


    ''' <summary>
    ''' Sets or gets the fade out time
    ''' </summary>
    ''' 
    Public Property FadeTime As TimeSpan
        Get
            Return CType(GetValue(FadeTimeProperty), TimeSpan)
        End Get
        Set(value As TimeSpan)
            SetValue(FadeTimeProperty, value)
        End Set
    End Property


    Public Sub New()

        InitializeComponent()
        AddHandler _timer.Tick, AddressOf Timer_Tick

    End Sub


    ''' <summary>
    ''' Display the specified message. Icon/iconColor default to the usual green checkmark for
    ''' success/confirmation toasts - pass e.g. "⚠ " with an amber/gray color for non-success hints.
    ''' </summary>
    '''
    Public Sub Show(message As String, Optional icon As String = "✔ ", Optional iconColor As Brush = Nothing)

        blkToastMessage.Text = message
        blkToastIcon.Text = icon
        blkToastIcon.Foreground = If(iconColor, CType(New BrushConverter().ConvertFromString("#FF89FCA6"), Brush))
        Me.Opacity = 1
        Me.Visibility = Visibility.Visible
        Me.BeginAnimation(OpacityProperty, Nothing)   'cancel any running fade
        _timer.Interval = DisplayTime
        _timer.Stop()
        _timer.Start()

    End Sub


    Private Sub Timer_Tick(sender As Object, e As EventArgs)

        _timer.Stop()
        Dim fade As New Animation.DoubleAnimation(0, FadeTime)
        AddHandler fade.Completed, Sub() Me.Visibility = Visibility.Collapsed
        Me.BeginAnimation(OpacityProperty, fade)

    End Sub


End Class
