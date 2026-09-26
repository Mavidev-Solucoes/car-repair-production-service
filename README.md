# car-repair-production-service
Esse projeto faz parte do Tech Challenge do curso de Arquitetura de Soluções da FIAP

## GitHub Actions CI - Secrets para SonarCloud

Para habilitar a análise no SonarCloud dentro do pipeline de CI, configure os seguintes secrets no repositório:

- `SONAR_TOKEN`: token de acesso do SonarCloud
- `SONAR_PROJECT_KEY`: chave do projeto no SonarCloud
- `SONAR_ORGANIZATION`: organização no SonarCloud

Sem esses secrets, o pipeline continua executando restore, build, testes, cobertura e validação de build Docker, mas pula a etapa de análise SonarCloud.

## GitHub Actions CD - Secrets para publicação no GHCR

Para publicar a imagem Docker no GitHub Container Registry (GHCR), o workflow de CD utiliza:

- `GITHUB_TOKEN` (automático do GitHub Actions): usado para autenticação no GHCR.

Não é necessário criar secrets adicionais para esse fluxo, desde que o workflow tenha permissão `packages: write`.
