using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using IT_Solution_Platform.Models;

namespace IT_Solution_Platform.Services
{
    public class ContactInquiryService
    {
        public readonly SupabaseDatabase _databaseService;
        public readonly AuditLogService _auditLog;

        public ContactInquiryService()
        {
            _databaseService = new SupabaseDatabase();
            _auditLog = new AuditLogService(_databaseService);
        }

        /// <summary>
        /// Creates a new contact inquiry in the database.
        /// </summary>
        /// <param name="inquiry">The contact inquriy to create</param>
        /// <param name="userId"> Optional user ID if the user is authenticated </param>
        /// <param name="ipAddress">IP address of the requester </param>
        /// <param name="userAgent">User agent of the requester</param>
        /// <return>True if successful, false otherwise</return>
        /// 
        public bool CreateInquiry(ContactInquiryModel inquiry, int? userId = null, string ipAddress = "Unknown", string userAgent = "Unknown")
        {
            try
            {
                var query = @"
                    INSERT INTO contact_inquiries (name, email, phone, subject, message, created_at)
                    VALUES (@name, @email, @phone, @subject, @message, @created_at)";

                inquiry.created_at = DateTime.UtcNow;

                var result = _databaseService.ExecuteNonQuery(query, inquiry);

                if (result > 0)
                {
                    _auditLog.LogAudit(
                      userId ?? 0,
                      "Contact Inquiry Created",
                      "Contact",
                      null,
                      new { inquiry.name, inquiry.email, inquiry.subject },
                      ipAddress,
                      userAgent
                  );
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _auditLog.LogAudit(
                   userId ?? 0,
                   "Contact Inquiry Creation Failed",
                   "Contact",
                   null,
                   new { Error = ex.Message, inquiry.name, inquiry.email },
                   ipAddress,
                   userAgent
               );
                throw;
            }
        }


        /// <summary>
        /// Gets all contact inquiries with pagination
        /// </summary>
        public List<ContactInquiryModel> GetInquiries(int page = 1, int pageSize = 20, bool unreadOnly = false)
        {
            try
            {
                var whereClause = unreadOnly ? "WHERE is_read = false OR is_read IS NULL" : "";
                var offset = (page - 1) * pageSize;

                var query = $@"
                    SELECT inquiry_id, name, email, phone, subject, message, 
                           is_read, responded_by, responded_at, created_at
                    FROM contact_inquiries 
                    {whereClause}
                    ORDER BY created_at DESC 
                    LIMIT @pageSize OFFSET @offset";

                return _databaseService.ExecuteQuery<ContactInquiryModel>(query, new { pageSize, offset });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetInquiries failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets a specific contact inquiry by ID
        /// </summary>
        public ContactInquiryModel GetInquiryById(int inquiryId)
        {
            try
            {
                var query = @"
                    SELECT inquiry_id, name, email, phone, subject, message, 
                           is_read, responded_by, responded_at, created_at
                    FROM contact_inquiries 
                    WHERE inquiry_id = @inquiryId";

                return _databaseService.ExecuteQuery<ContactInquiryModel>(query, new { inquiryId }).FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetInquiryById failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Marks an inquiry as read
        /// </summary>
        public bool MarkAsRead(int inquiryId, int respondedBy)
        {
            try
            {
                var query = @"
                    UPDATE contact_inquiries 
                    SET is_read = true, responded_by = @respondedBy, responded_at = @respondedAt
                    WHERE inquiry_id = @inquiryId";

                var result = _databaseService.ExecuteNonQuery(query, new
                {
                    inquiryId,
                    respondedBy,
                    respondedAt = DateTime.UtcNow
                });

                if (result > 0)
                {
                    _auditLog.LogAudit(
                        respondedBy,
                        "Contact Inquiry Marked as Read",
                        "Contact",
                        inquiryId,
                        new { inquiryId },
                        "System",
                        "System"
                    );
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] MarkAsRead failed: {ex.Message}");
                throw;
            }
        }
    }
}