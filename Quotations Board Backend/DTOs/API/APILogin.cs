using System.ComponentModel.DataAnnotations;

namespace QuotationsBoardBackend.DTOs.API
{
    public class APILogin
    {
        [Required]
        public string ClientId { get; set; } = null!;
        [Required]
        public string ClientSecret { get; set; } = null!;
    }
}