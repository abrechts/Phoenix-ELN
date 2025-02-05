﻿Imports ElnBase
Imports System.Windows

Public Class MainStatusBar


    Public Event RequestReconnect(sender As Object)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Sets if a server error should be displayed or not
    ''' </summary>
    ''' 
    Public Property DisplayServerError() As Boolean

        Get
            Return pnlServerError.IsVisible
        End Get

        Set(value As Boolean)

            ServerSync.IsServerConnectionLost = value

            If value = True Then

                pnlServerError.Visibility = Visibility.Visible
                pnlServerOk.Visibility = Visibility.Collapsed
                If My.Settings.IsServerOffByUser Then
                    icoServerError.ToolTip = "ELN server disconnected by user."
                Else
                    icoServerError.ToolTip = "The ELN server currently is unavailable!"
                End If

            Else

                pnlServerError.Visibility = Visibility.Collapsed
                pnlServerOk.Visibility = Visibility.Visible

            End If

        End Set

    End Property


    Private Sub lnkReconnect_PreviewMouseUp() Handles lnkReconnects.PreviewMouseUp

        RaiseEvent RequestReconnect(Me)

    End Sub


    Public Sub AnimateSaveIcon()

        'animate save label
        Dim anim As New Animations
        anim.FadeInAndOut(lblSaving, peakOpacity:=0.8, fadeMillisec:=300, holdMillisec:=200)

    End Sub


End Class
