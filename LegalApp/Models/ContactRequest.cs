using System.ComponentModel.DataAnnotations;

namespace LegalApp.Models;

public class ContactRequest
{
    [Required] public string Situation { get; set; } = "";
    [Required] public string Format { get; set; } = "";
    public string? Urgency { get; set; }
    public string? Budget { get; set; }
    [Required] public string Name { get; set; } = "";
    [Required] public string Phone { get; set; } = "";
    [EmailAddress] public string? Email { get; set; }
    public string? Comment { get; set; }
    public bool Consent { get; set; }
}