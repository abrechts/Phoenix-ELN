Imports System.Windows
Imports System.Windows.Media.Animation

Public Class Animations

    ''' <summary>
    ''' Fades the specified control in, holds it for the specified time at the specified peak opacity, and subsequently fades it out again.
    ''' </summary>
    ''' <param name="targetElement">The FrameworkElement to apply the animation to.</param>
    ''' <param name="peakOpacity">The maximum opacity to reach (i.e. at hold level).</param>
    ''' <param name="fadeMillisec">The ramp time in milliseconds to fade the control in and out.</param>
    ''' <param name="holdMillisec">The time to hold the control at the specified peak opacity.</param>
    ''' 
    Public Sub FadeInAndOut(targetElement As FrameworkElement, peakOpacity As Double, fadeMillisec As Integer, holdMillisec As Integer)

        Dim sb = New Storyboard()

        Dim anim1 = New DoubleAnimation()
        With anim1
            .BeginTime = New TimeSpan(0, 0, 0)
            .From = 0
            .To = peakOpacity
            .Duration = TimeSpan.FromMilliseconds(fadeMillisec)
        End With
        sb.Children.Add(anim1)

        Dim anim2 = New DoubleAnimation()
        With anim2
            .BeginTime = TimeSpan.FromMilliseconds(fadeMillisec + holdMillisec)
            .From = peakOpacity
            .To = 0
            .Duration = TimeSpan.FromMilliseconds(fadeMillisec)
        End With
        sb.Children.Add(anim2)

        Storyboard.SetTarget(sb, targetElement)
        Storyboard.SetTargetProperty(sb, New PropertyPath(FrameworkElement.OpacityProperty))

        sb.Begin()

    End Sub


End Class
