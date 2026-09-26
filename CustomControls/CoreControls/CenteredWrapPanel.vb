Imports System.Windows
Imports System.Windows.Controls

''' <summary>
''' A horizontal wrap panel that centers each wrapped line, unlike the stock WrapPanel (which always
''' left-aligns every line regardless of how much space is left over). Used for dlgMaterialSearch's
''' filter chip list, so a handful of chips read as a centered group rather than hugging the left edge.
''' </summary>
'''
Public Class CenteredWrapPanel

    Inherits Panel

    Protected Overrides Function MeasureOverride(availableSize As Size) As Size

        Dim lineWidth As Double = 0
        Dim lineHeight As Double = 0
        Dim totalHeight As Double = 0
        Dim maxLineWidth As Double = 0

        For Each child As UIElement In InternalChildren

            child.Measure(New Size(availableSize.Width, Double.PositiveInfinity))
            Dim childSize = child.DesiredSize

            If lineWidth + childSize.Width > availableSize.Width AndAlso lineWidth > 0 Then
                totalHeight += lineHeight
                maxLineWidth = Math.Max(maxLineWidth, lineWidth)
                lineWidth = 0
                lineHeight = 0
            End If

            lineWidth += childSize.Width
            lineHeight = Math.Max(lineHeight, childSize.Height)

        Next

        totalHeight += lineHeight
        maxLineWidth = Math.Max(maxLineWidth, lineWidth)

        Return New Size(If(Double.IsInfinity(availableSize.Width), maxLineWidth, availableSize.Width), totalHeight)

    End Function


    Protected Overrides Function ArrangeOverride(finalSize As Size) As Size

        Dim lineChildren As New List(Of UIElement)
        Dim lineWidth As Double = 0
        Dim lineHeight As Double = 0
        Dim y As Double = 0

        For Each child As UIElement In InternalChildren

            Dim childSize = child.DesiredSize

            If lineWidth + childSize.Width > finalSize.Width AndAlso lineChildren.Count > 0 Then
                ArrangeLine(lineChildren, lineWidth, lineHeight, y, finalSize.Width)
                y += lineHeight
                lineChildren.Clear()
                lineWidth = 0
                lineHeight = 0
            End If

            lineChildren.Add(child)
            lineWidth += childSize.Width
            lineHeight = Math.Max(lineHeight, childSize.Height)

        Next

        If lineChildren.Count > 0 Then
            ArrangeLine(lineChildren, lineWidth, lineHeight, y, finalSize.Width)
        End If

        Return finalSize

    End Function


    Private Sub ArrangeLine(lineChildren As List(Of UIElement), lineWidth As Double, lineHeight As Double, y As Double, availableWidth As Double)

        Dim x = Math.Max(0, (availableWidth - lineWidth) / 2)

        For Each child In lineChildren
            child.Arrange(New Rect(x, y, child.DesiredSize.Width, lineHeight))
            x += child.DesiredSize.Width
        Next

    End Sub

End Class
