namespace CqrsGenerator.Core.Templates;

public static class TemplateCatalog
{
    public const string Query = """
using Application.Common.Interfaces;
{{ usings_block }}

namespace {{ namespace }}
{
    public class {{ query_type }} : IQuery<{{ response_type }}>
    {
{{ if has_properties }}        public {{ query_type }}({{ constructor_parameters }})
        {
{{ constructor_assignments }}        }

{{ properties_block }}
{{ end }}    }
}
""";

    public const string Handler = """
using MediatR;
using System;
{{ if needs_collections }}using System.Collections.Generic;
{{ end }}using System.Threading;
using System.Threading.Tasks;
{{ if dto_namespace != "" }}using {{ dto_namespace }};
{{ end }}{{ if service_namespace != "" }}using {{ service_namespace }};
{{ end }}
namespace {{ namespace }}
{
    public class {{ handler_type }} : IRequestHandler<{{ query_type }}, {{ response_type }}>
    {
{{ if has_service }}        private readonly {{ service_type }} {{ service_field }};

        public {{ handler_type }}({{ service_type }} {{ service_parameter }})
        {
            {{ service_field }} = {{ service_parameter }};
        }

{{ end }}        public async Task<{{ response_type }}> Handle({{ query_type }} request, CancellationToken cancellationToken)
        {
            {{ handler_body }}
        }
    }
}
""";

    public const string Dto = """
namespace {{ namespace }}
{
    public class {{ dto_type }}
    {
    }
}
""";

    public const string WebPage = """"
@page "{{ route }}"
@inherits StateComponentBase

@inject AppMediator Mediator

<LoadingWrapper IsLoading="@State.IsBusy("loading-{{ busy_key }}")">
    <RadzenStack Gap="1rem">
{{ if has_queries }}
        <RadzenCard>
            <DataGrid Data="@{{ queries[0].variable_name }}"
                      TItem="{{ queries[0].result_type }}"
                      AllowPaging="true"
                      AllowColumnResize="true">
                <Columns>
                    <RadzenDataGridColumn TItem="{{ queries[0].result_type }}"
                                          Property="Id"
                                          Title="ID" />
                </Columns>
            </DataGrid>
        </RadzenCard>
{{ end }}
    </RadzenStack>
</LoadingWrapper>

@code {
{{ for param in route_parameters }}
    [Parameter]
    public {{ param.param_type }} {{ param.name }} { get; set; }
{{ end }}

{{ if has_queries }}
    private bool _isFirstLoad = true;
{{ for q in queries }}
    private {{ if q.is_list }}IEnumerable<{{ q.result_type }}>{{ else }}{{ q.result_type }}{{ end }} {{ q.variable_name }}{{ if q.is_list }} = []{{ else }} = default!{{ end }};
{{ end }}

    protected override async Task OnInitializedAsync()
    {
        if (_isFirstLoad)
        {
            _isFirstLoad = false;
            await State.RunAsync("loading-{{ busy_key }}", async () =>
            {
{{ for q in queries }}
                {{ q.variable_name }} = await Mediator.SendAsync(new {{ q.query_name }}({{ q.args }}));
{{ end }}
            });
        }
    }

{{ for q in queries }}{{ if q.has_refresh }}
    private async Task {{ q.refresh_method }}() =>
        {{ q.variable_name }} = await Mediator.SendAsync(new {{ q.query_name }}({{ q.args }}));
{{ end }}{{ end }}
{{ else }}
    protected override async Task OnInitializedAsync()
    {
        // await Mediator.SendAsync(new YourQuery());
    }
{{ end }}
}
"""";

    public const string WebImports = """
@using {{ application_feature_namespace }}.DTOs
@using {{ application_feature_namespace }}.Queries
@using {{ web_feature_namespace }}.Components
""";

    public static string GetBuiltIn(string name) => name switch
    {
        TemplateNames.Query => Query,
        TemplateNames.Handler => Handler,
        TemplateNames.Dto => Dto,
        TemplateNames.WebPage => WebPage,
        TemplateNames.WebImports => WebImports,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown template name."),
    };
}
