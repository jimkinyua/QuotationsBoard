using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class InstitutionApplication
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = null!;
    public string InstitutionName { get; set; } = null!;
    public string ApplicationStatus { get; set; } = InstitutionApplicationStatus.Open;
    public string AdministratorEmail { get; set; } = null!;
    public string AdministratorPhoneNumber { get; set; } = null!;
    public string AdministratorName { get; set; } = null!;
    public string InstitutionAddress { get; set; } = null!;
    public DateTime ApplicationDate { get; set; } = DateTime.Now;
    public string InstitutionType { get; set; } = null!;

}