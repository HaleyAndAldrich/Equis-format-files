''' <summary>
'''   $Header: /EarthSoft_2.0/EDP/Formats/data_tables.vb   2   2007-12-07 09:23:58-07:00   mweaver $
'''		$UTCDate: 2007-12-07 16:23:58Z $
''' </summary>
Option Strict Off

Imports EarthSoft.Common
Imports EarthSoft.Edp
Imports System
Imports System.Collections

''' <summary>
'''     This is the parent class that will handle the EFWEDD (4-File) format
''' </summary>
'''
Public Class DataTablesHandler
	Inherits EarthSoft.Edp.EddCustomHandler

	Public Overrides Property Err() As EddError
		Get
			Return MyBase.Err
		End Get
		Set(ByVal Value As EddError)
			Me._Err = Value
			'TODO: set the Warning status where applicable
		End Set
	End Property

	Public Sub New()
		' call the base class constructor
		MyBase.New()
	End Sub

	Public Function GetFacilityId(ByVal eddRow As System.Data.DataRow, ByVal targetRow As System.Data.DataRow) As Object
		Try
			Return targetRow.Table.DataSet.Tables.Item("dt_facility").Select("facility_code ='" & eddRow.Item("facility_code").ToString().Replace("'", "''") & "'")(0).Item("facility_id")
		Catch ex As Exception
			Return -1
		End Try
  End Function

  Public Function GetBasicResultDepth(ByVal eddRow As System.Data.DataRow, ByVal targetRow As System.Data.DataRow) As Object
    If targetRow.Table.Columns.Contains("end_depth") Then
      targetRow.Item("end_depth") = eddRow.Item("end_depth")
    End If
    If targetRow.Table.Columns.Contains("sys_sample_code") Then
      targetRow.Item("sys_sample_code") = eddRow.Item("sys_sample_code")
    End If
    If targetRow.Table.Columns.Contains("reportable_yn") Then
      targetRow.Item("reportable_yn") = eddRow.Item("reportable_yn")
    End If
    Return eddRow.Item("depth")
  End Function

End Class
