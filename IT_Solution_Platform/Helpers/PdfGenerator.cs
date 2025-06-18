using System;
using System.IO;
using System.Linq;
using IT_Solution_Platform.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json;

namespace IT_Solution_Platform.Helpers
{
    public class PdfGenerator
    {
        public static byte[] GenerateReceiptPdf(ServiceRequestDetailViewModel order)
        {
            using (var memoryStream = new MemoryStream())
            {
                // Create PDF document
                var document = new Document(PageSize.A4, 40, 40, 40, 40);
                var writer = PdfWriter.GetInstance(document, memoryStream);

                document.Open();

                // Add content to PDF
                AddReceiptContent(document, order);

                document.Close();
                return memoryStream.ToArray();
            }
        }

        private static void AddReceiptContent(Document document, ServiceRequestDetailViewModel order)
        {
            // Parse request details if available
            dynamic requestDetails = null;
            if (!string.IsNullOrEmpty(order.request_details))
            {
                try
                {
                    requestDetails = JsonConvert.DeserializeObject(order.request_details);
                }
                catch { }
            }

            // Define fonts
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, BaseColor.DARK_GRAY);
            var companyFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, new BaseColor(0, 102, 204)); // Blue color
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
            var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(51, 51, 51));
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
            var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.BLACK);
            var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.GRAY);

            // Company Header with Logo placeholder
            var headerTable = new PdfPTable(2);
            headerTable.WidthPercentage = 100;
            headerTable.SetWidths(new float[] { 3f, 1f });
            headerTable.SpacingAfter = 20f;

            // Left side - Company info
            var companyCell = new PdfPCell();
            companyCell.Border = Rectangle.NO_BORDER;
            companyCell.AddElement(new Paragraph("SecuDev Solutions", companyFont));
            companyCell.AddElement(new Paragraph("Professional IT Services", normalFont));
            companyCell.AddElement(new Paragraph("support@secudev.com | +962 (79) 509-5985", smallFont));
            headerTable.AddCell(companyCell);

            // Right side - Receipt info
            var receiptInfoCell = new PdfPCell();
            receiptInfoCell.Border = Rectangle.NO_BORDER;
            receiptInfoCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            receiptInfoCell.AddElement(new Paragraph("SERVICE RECEIPT", headerFont));
            receiptInfoCell.AddElement(new Paragraph($"Receipt #{order.request_id:D6}", boldFont));
            receiptInfoCell.AddElement(new Paragraph($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC", smallFont));
            headerTable.AddCell(receiptInfoCell);

            document.Add(headerTable);

            // Add horizontal line
            var line = new Paragraph("_".PadRight(80, '_'), smallFont);
            line.Alignment = Element.ALIGN_CENTER;
            line.SpacingAfter = 20f;
            document.Add(line);

            // Order Summary Section
            var orderSummaryTitle = new Paragraph("ORDER SUMMARY", subtitleFont);
            orderSummaryTitle.SpacingAfter = 10f;
            document.Add(orderSummaryTitle);

            var summaryTable = new PdfPTable(4);
            summaryTable.WidthPercentage = 100;
            summaryTable.SetWidths(new float[] { 1.5f, 2f, 1.5f, 1.5f });
            summaryTable.SpacingAfter = 20f;

            // Summary table headers
            AddHeaderCell(summaryTable, "Order ID", boldFont);
            AddHeaderCell(summaryTable, "Service", boldFont);
            AddHeaderCell(summaryTable, "Status", boldFont);
            AddHeaderCell(summaryTable, "Amount", boldFont);

            // Summary table data
            AddDataCell(summaryTable, $"#{order.request_id}", normalFont);
            AddDataCell(summaryTable, order.service_name ?? "N/A", normalFont);

            // Status cell with background color
            var statusCell = new PdfPCell(new Phrase(order.status_name ?? "N/A", normalFont));
            statusCell.BackgroundColor = GetStatusColor(order.status_name);
            statusCell.HorizontalAlignment = Element.ALIGN_CENTER;
            statusCell.Padding = 5f;
            summaryTable.AddCell(statusCell);

            AddDataCell(summaryTable, order.FormattedTotalAmount ?? "$0.00", boldFont);

            document.Add(summaryTable);

            // Order Details Section
            var orderDetailsTitle = new Paragraph("ORDER DETAILS", subtitleFont);
            orderDetailsTitle.SpacingAfter = 10f;
            document.Add(orderDetailsTitle);

            var orderTable = new PdfPTable(2);
            orderTable.WidthPercentage = 100;
            orderTable.SetWidths(new float[] { 1.2f, 2f });
            orderTable.SpacingAfter = 20f;

            // Order Details
            AddStyledTableRow(orderTable, "Service Category:", order.category_name ?? "N/A", boldFont, normalFont);
            AddStyledTableRow(orderTable, "Priority Level:", order.PriorityText ?? "Standard", boldFont, normalFont);
            AddStyledTableRow(orderTable, "Request Date:", order.FormattedRequestedDate ?? "N/A", boldFont, normalFont);

            if (!string.IsNullOrEmpty(order.FormattedApprovedDate))
            {
                AddStyledTableRow(orderTable, "Approved Date:", order.FormattedApprovedDate, boldFont, normalFont);
            }

            if (!string.IsNullOrEmpty(order.FormattedCompletionDate))
            {
                AddStyledTableRow(orderTable, "Completion Date:", order.FormattedCompletionDate, boldFont, normalFont);
            }

            AddStyledTableRow(orderTable, "Progress:", $"{order.ProgressPercentage}%", boldFont, normalFont);

            document.Add(orderTable);

            // Customer Information
            if (!string.IsNullOrEmpty(order.UserFullName))
            {
                var customerTitle = new Paragraph("CUSTOMER INFORMATION", subtitleFont);
                customerTitle.SpacingAfter = 10f;
                document.Add(customerTitle);

                var customerTable = new PdfPTable(2);
                customerTable.WidthPercentage = 100;
                customerTable.SetWidths(new float[] { 1.2f, 2f });
                customerTable.SpacingAfter = 20f;

                AddStyledTableRow(customerTable, "Customer Name:", order.UserFullName, boldFont, normalFont);
                AddStyledTableRow(customerTable, "Email Address:", order.user_email ?? "N/A", boldFont, normalFont);
                AddStyledTableRow(customerTable, "Customer ID:", $"#{order.user_id}", boldFont, normalFont);

                document.Add(customerTable);
            }

            // Service Specifications
            if (requestDetails != null)
            {
                var serviceTitle = new Paragraph("SERVICE SPECIFICATIONS", subtitleFont);
                serviceTitle.SpacingAfter = 10f;
                document.Add(serviceTitle);

                var serviceTable = new PdfPTable(2);
                serviceTable.WidthPercentage = 100;
                serviceTable.SetWidths(new float[] { 1.2f, 2f });
                serviceTable.SpacingAfter = 20f;

                // Add service-specific details based on type
                AddServiceSpecificDetails(serviceTable, requestDetails, boldFont, normalFont);

                document.Add(serviceTable);
            }

            // Approval Information
            if (!string.IsNullOrEmpty(order.ApproverFullName) && order.ApproverFullName != "System")
            {
                var approvalTitle = new Paragraph("APPROVAL INFORMATION", subtitleFont);
                approvalTitle.SpacingAfter = 10f;
                document.Add(approvalTitle);

                var approvalTable = new PdfPTable(2);
                approvalTable.WidthPercentage = 100;
                approvalTable.SetWidths(new float[] { 1.2f, 2f });
                approvalTable.SpacingAfter = 20f;

                AddStyledTableRow(approvalTable, "Approved By:", order.ApproverFullName, boldFont, normalFont);
                AddStyledTableRow(approvalTable, "Approver Email:", order.approver_email ?? "N/A", boldFont, normalFont);
                AddStyledTableRow(approvalTable, "Approver ID:", $"#{order.approved_by}", boldFont, normalFont);

                document.Add(approvalTable);
            }

            // Service Description
            if (!string.IsNullOrEmpty(order.service_description))
            {
                var descTitle = new Paragraph("SERVICE DESCRIPTION", subtitleFont);
                descTitle.SpacingAfter = 10f;
                document.Add(descTitle);

                var descParagraph = new Paragraph(order.service_description.Trim(), normalFont);
                descParagraph.SpacingAfter = 20f;
                descParagraph.Alignment = Element.ALIGN_JUSTIFIED;
                document.Add(descParagraph);
            }

            // Internal Notes (if any)
            if (!string.IsNullOrEmpty(order.notes) && order.notes != "Submitted via web form")
            {
                var notesTitle = new Paragraph("INTERNAL NOTES", subtitleFont);
                notesTitle.SpacingAfter = 10f;
                document.Add(notesTitle);

                var notesParagraph = new Paragraph(order.notes, normalFont);
                notesParagraph.SpacingAfter = 20f;
                notesParagraph.Alignment = Element.ALIGN_JUSTIFIED;
                document.Add(notesParagraph);
            }

            // Footer with generation info
            document.Add(new Paragraph("\n"));

            var footerTable = new PdfPTable(2);
            footerTable.WidthPercentage = 100;
            footerTable.SetWidths(new float[] { 1f, 1f });

            var leftFooterCell = new PdfPCell(new Phrase("Thank you for choosing SecuDev Solutions!", smallFont));
            leftFooterCell.Border = Rectangle.TOP_BORDER;
            leftFooterCell.BorderColor = BaseColor.LIGHT_GRAY;
            leftFooterCell.PaddingTop = 10f;
            leftFooterCell.HorizontalAlignment = Element.ALIGN_LEFT;
            footerTable.AddCell(leftFooterCell);

            var rightFooterCell = new PdfPCell(new Phrase($"Generated by: SecuDev Company | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC", smallFont));
            rightFooterCell.Border = Rectangle.TOP_BORDER;
            rightFooterCell.BorderColor = BaseColor.LIGHT_GRAY;
            rightFooterCell.PaddingTop = 10f;
            rightFooterCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            footerTable.AddCell(rightFooterCell);

            document.Add(footerTable);
        }

        private static void AddHeaderCell(PdfPTable table, string text, Font font)
        {
            var cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = new BaseColor(240, 240, 240);
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 8f;
            table.AddCell(cell);
        }

        private static void AddDataCell(PdfPTable table, string text, Font font)
        {
            var cell = new PdfPCell(new Phrase(text, font));
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.Padding = 8f;
            table.AddCell(cell);
        }

        private static void AddStyledTableRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            var labelCell = new PdfPCell(new Phrase(label, labelFont));
            labelCell.BackgroundColor = new BaseColor(250, 250, 250);
            labelCell.Border = Rectangle.BOX;
            labelCell.BorderColor = BaseColor.LIGHT_GRAY;
            labelCell.PaddingLeft = 10f;
            labelCell.PaddingTop = 8f;
            labelCell.PaddingBottom = 8f;
            table.AddCell(labelCell);

            var valueCell = new PdfPCell(new Phrase(value, valueFont));
            valueCell.Border = Rectangle.BOX;
            valueCell.BorderColor = BaseColor.LIGHT_GRAY;
            valueCell.PaddingLeft = 10f;
            valueCell.PaddingTop = 8f;
            valueCell.PaddingBottom = 8f;
            table.AddCell(valueCell);
        }

        private static BaseColor GetStatusColor(string status)
        {
            switch (status?.ToLower())
            {
                case "completed":
                    return new BaseColor(144, 238, 144); // Light green
                case "in progress":
                case "approved":
                    return new BaseColor(173, 216, 230); // Light blue
                case "submitted":
                case "pending":
                    return new BaseColor(255, 255, 224); // Light yellow
                case "cancelled":
                case "rejected":
                    return new BaseColor(255, 182, 193); // Light red
                default:
                    return BaseColor.WHITE;
            }
        }

        private static void AddServiceSpecificDetails(PdfPTable table, dynamic details, Font boldFont, Font normalFont)
        {
            try
            {
                // Handle different service types
                if (details.TargetSystem != null && details.TestingObjectives != null)
                {
                    // Penetration Testing
                    AddStyledTableRow(table, "Target System:", details.TargetSystem?.ToString() ?? "N/A", boldFont, normalFont);
                    AddStyledTableRow(table, "Testing Objectives:", FormatEnumValue(details.TestingObjectives?.ToString()), boldFont, normalFont);

                    if (details.ScopeDescription != null)
                        AddStyledTableRow(table, "Scope Description:", details.ScopeDescription.ToString(), boldFont, normalFont);

                    if (details.ComplianceRequirements != null)
                        AddStyledTableRow(table, "Compliance:", details.ComplianceRequirements.ToString().ToUpper(), boldFont, normalFont);

                    if (details.PreferredStartDate != null)
                        AddStyledTableRow(table, "Preferred Start:", FormatDate(details.PreferredStartDate.ToString()), boldFont, normalFont);

                    if (details.PreferredEndDate != null)
                        AddStyledTableRow(table, "Preferred End:", FormatDate(details.PreferredEndDate.ToString()), boldFont, normalFont);
                }
                else if (details.ProjectName != null && details.Platform != null)
                {
                    // Mobile & Web Development
                    AddStyledTableRow(table, "Project Name:", details.ProjectName?.ToString() ?? "N/A", boldFont, normalFont);
                    AddStyledTableRow(table, "Platform:", FormatEnumValue(details.Platform?.ToString()), boldFont, normalFont);
                    AddStyledTableRow(table, "Development Type:", FormatEnumValue(details.DevelopmentType?.ToString()), boldFont, normalFont);

                    if (details.ProjectDescription != null)
                        AddStyledTableRow(table, "Description:", details.ProjectDescription.ToString(), boldFont, normalFont);

                    if (details.PreferredStartDate != null)
                        AddStyledTableRow(table, "Start Date:", FormatDate(details.PreferredStartDate.ToString()), boldFont, normalFont);

                    if (details.PreferredEndDate != null)
                        AddStyledTableRow(table, "End Date:", FormatDate(details.PreferredEndDate.ToString()), boldFont, normalFont);
                }
                else if (details.RequestType != null && details.Department != null)
                {
                    // Network Setup
                    AddStyledTableRow(table, "Request Type:", FormatEnumValue(details.RequestType?.ToString()), boldFont, normalFont);
                    AddStyledTableRow(table, "Department:", details.Department?.ToString() ?? "N/A", boldFont, normalFont);
                    AddStyledTableRow(table, "Location:", details.Location?.ToString() ?? "N/A", boldFont, normalFont);

                    if (details.RoomNumber != null)
                        AddStyledTableRow(table, "Room Number:", details.RoomNumber.ToString(), boldFont, normalFont);

                    if (details.NumberOfPorts != null)
                        AddStyledTableRow(table, "Number of Ports:", details.NumberOfPorts.ToString(), boldFont, normalFont);

                    if (details.PortType != null)
                        AddStyledTableRow(table, "Port Type:", details.PortType.ToString(), boldFont, normalFont);

                    if (details.NetworkSpeed != null)
                        AddStyledTableRow(table, "Network Speed:", FormatNetworkSpeed(details.NetworkSpeed.ToString()), boldFont, normalFont);

                    if (details.VlanAssignment != null)
                        AddStyledTableRow(table, "VLAN Assignment:", details.VlanAssignment.ToString(), boldFont, normalFont);

                    if (details.WirelessAccessRequired != null)
                        AddStyledTableRow(table, "Wireless Access:", details.WirelessAccessRequired.ToString() == "True" ? "Yes" : "No", boldFont, normalFont);

                    if (details.SpecialSecurityRequired != null)
                        AddStyledTableRow(table, "Special Security:", details.SpecialSecurityRequired.ToString() == "True" ? "Yes" : "No", boldFont, normalFont);

                    if (details.HardwareInstallationRequired != null)
                        AddStyledTableRow(table, "Hardware Installation:", details.HardwareInstallationRequired.ToString() == "True" ? "Yes" : "No", boldFont, normalFont);

                    if (details.RequestedCompletionDate != null)
                        AddStyledTableRow(table, "Requested Completion:", FormatDate(details.RequestedCompletionDate.ToString()), boldFont, normalFont);

                    if (details.PreferredInstallationTime != null)
                        AddStyledTableRow(table, "Installation Time:", FormatEnumValue(details.PreferredInstallationTime.ToString()), boldFont, normalFont);

                    if (details.IsUrgent != null)
                        AddStyledTableRow(table, "Urgent Request:", details.IsUrgent.ToString() == "True" ? "Yes" : "No", boldFont, normalFont);

                    if (details.BusinessJustification != null)
                        AddStyledTableRow(table, "Business Justification:", details.BusinessJustification.ToString(), boldFont, normalFont);

                    if (details.BudgetCode != null)
                        AddStyledTableRow(table, "Budget Code:", details.BudgetCode.ToString(), boldFont, normalFont);
                }

                // Common contact fields for all service types
                if (details.PrimaryContactName != null)
                    AddStyledTableRow(table, "Primary Contact:", details.PrimaryContactName.ToString(), boldFont, normalFont);

                if (details.PrimaryContactEmail != null)
                    AddStyledTableRow(table, "Contact Email:", details.PrimaryContactEmail.ToString(), boldFont, normalFont);

                if (details.PrimaryContactPhone != null)
                    AddStyledTableRow(table, "Contact Phone:", details.PrimaryContactPhone.ToString(), boldFont, normalFont);

                if (details.ManagerName != null)
                    AddStyledTableRow(table, "Manager:", details.ManagerName.ToString(), boldFont, normalFont);

                if (details.ManagerEmail != null)
                    AddStyledTableRow(table, "Manager Email:", details.ManagerEmail.ToString(), boldFont, normalFont);

                if (details.AdditionalNotes != null)
                    AddStyledTableRow(table, "Additional Notes:", details.AdditionalNotes.ToString(), boldFont, normalFont);
            }
            catch (Exception ex)
            {
                // Log error or handle gracefully
                AddStyledTableRow(table, "Service Details:", "Unable to parse service details", boldFont, normalFont);
            }
        }

        private static string FormatEnumValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "N/A";

            return value
                .Replace("-", " ")
                .Replace("_", " ")
                .Split(' ')
                .Select(word => char.ToUpper(word[0]) + (word.Length > 1 ? word.Substring(1).ToLower() : ""))
                .Aggregate((a, b) => a + " " + b);
        }

        private static string FormatNetworkSpeed(string speed)
        {
            if (string.IsNullOrEmpty(speed)) return "N/A";

            return speed
                .Replace("Speed", "")
                .Replace("Mbps", " Mbps")
                .Replace("Gbps", " Gbps")
                .Trim();
        }

        private static string FormatDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString)) return "N/A";

            if (DateTime.TryParse(dateString, out DateTime date))
            {
                return date.ToString("MMMM dd, yyyy");
            }

            return dateString;
        }
    }
}