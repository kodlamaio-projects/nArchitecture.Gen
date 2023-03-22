using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Generate.Constants;

public static class GenerateBusinessMessages
{
    public static string EntityClassShouldBeInheritEntityBaseClass(string entityName) =>
        $"{entityName} class must be inherit Entity base class.";
}
