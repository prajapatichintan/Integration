<%@ Page Title="4-Tell Boost: Cart Data Extractor" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" Inherits="_Default" MaintainScrollPositionOnPostback="true" Codebehind="Default.aspx.cs" %>

<asp:Content ID="HeaderContent" runat="server" ContentPlaceHolderID="HeadContent">
	<style type="text/css">
		.style1
		{
			width: 315px;
		}
  	.style2
		{
			width: 3px;
			height: 468px;
		}
		.style3
		{
			height: 468px;
		}
  </style>
</asp:Content>
<asp:Content ID="BodyContent" runat="server" ContentPlaceHolderID="MainContent">
	<asp:ScriptManager ID="ScriptManager1" runat="server">
	</asp:ScriptManager>
	<table class="uploadtablestyle">
			<tr>
        <td class="blankcolumn">&nbsp;</td>
				<td class="style1" >Client Alias:&nbsp;&nbsp;&nbsp;&nbsp;
					<asp:DropDownList ID="DropDownListClientAlias" runat="server" 
                    AutoPostBack="True" Width="108px"
                    onselectedindexchanged="DropDownListClientAlias_SelectedIndexChanged">
                </asp:DropDownList>&nbsp;
					<asp:TextBox ID="TextBox_clientAlias" runat="server" CssClass="uploadTextEntry" 
						Width="100px"></asp:TextBox>
				</td>
				<td class="uploadbutton">
					<asp:Button ID="Button_Export" runat="server" Text="Export All" Width="30%"
						onclick="Button_export_Click" />
					<asp:Button ID="Button_ExportSelected" runat="server" Text="Export Selected Options" Width="50%"
						onclick="Button_selections_Click" />
				</td>
			</tr>
			<tr>
        <td class="blankcolumn">&nbsp;</td>
        <td class="style1">
					<asp:Button ID="Button_UpdateAllClients" runat="server" 
						Text="Update All Clients" Width="50%"
						onclick="Button_UpdateAllClients_Click" Visible="False" />
					<asp:Button ID="Button_ResetClientList" runat="server" 
						Text="Reset Client List" Width="50%" onclick="Button_ResetClientList_Click" />
					<asp:Button ID="Button_CancelThread" runat="server" 
						Text="Cancel Thread" Width="40%" onclick="Button_CancelThread_Click" />
				</td>
        <td class="selections" align="right">
					<asp:CheckBox ID="Checkbox_AllSales" Text="All Sales" runat="server" 
						oncheckedchanged="Checkbox_AllSales_CheckedChanged" />
					<asp:CheckBox ID="Checkbox_SalesUpdate" Text="Sales Update" runat="server" 
						Checked="True" oncheckedchanged="Checkbox_SalesUpdate_CheckedChanged" />
					<asp:CheckBox ID="Checkbox_Catalog" Text="Catalog" runat="server" Checked="True" />
			</tr>
			<tr>
        <td colspan="3">&nbsp;</td>
			</tr>
		</table>
		<hr />
		<table class="uploadtablestyle">
			<tr>	
				<td class="blankcolumn">&nbsp;</td>
				<td class="uploadlabel">Results:</td>
			</tr>
			<tr>
        <td class="style2"></td>
				<td class="style3">
				<asp:UpdatePanel ID="UpdatePanel_Results" runat="server" UpdateMode="Conditional">
         <ContentTemplate>
 					<asp:Timer ID="ProgressTimer" runat="server" ontick="ProgressTimer_Tick">
					</asp:Timer>		
					<asp:TextBox ID="TextBox_result" runat="server" ReadOnly="True" 
						TextMode="MultiLine" Height="500px" Width="100%"></asp:TextBox>
          </ContentTemplate>
				</asp:UpdatePanel>
				</td>
			</tr>
		</table>
</asp:Content>
