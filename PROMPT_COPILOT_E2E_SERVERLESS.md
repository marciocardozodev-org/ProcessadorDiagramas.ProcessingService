# Prompt Copilot: Fechamento E2E Serverless com IAM Restrito

## Objetivo
Este documento entrega um prompt pronto para o Copilot do outro microservico, incorporando as licoes praticas que funcionaram aqui para fechar E2E serverless mesmo com restricoes de IAM/ambiente.

## Como resolvemos os problemas (mapeamento direto)

### 1) EKS bloqueado (`eks:DescribeCluster`)
Problema deles:
- Runner sem permissao de `eks:DescribeCluster`
- Port-forward e validacoes via cluster ficaram instaveis

Como resolvemos aqui:
- Removemos dependencia obrigatoria de EKS no caminho principal do E2E CI.
- Mantivemos fallback para validacao de banco em ambiente com acesso (quando disponivel), mas o fluxo principal valida por contrato funcional (evento final + status persistido).

Diretriz para eles:
- Nao acoplar gate principal de CI a acesso EKS.
- Tratar validacao SQL direta como etapa complementar/diagnostica.

### 2) SNS temporario bloqueado (`sns:CreateTopic`)
Problema deles:
- Nao conseguem criar topico temporario por execucao.

Como resolvemos aqui:
- Usamos topico fixo pre-provisionado.
- Subscricao/fila fixa de observabilidade para E2E.
- Correlacao forte (`analysisId` + `correlationId`) para separar execucoes sem isolamento fisico por topico.

Diretriz para eles:
- Migrar de topicos efemeros para recursos estaticos por ambiente.

### 3) SQS nao deterministica na CI (`NonExistentQueue`/acesso)
Problema deles:
- Criacao/listagem de filas variou por politica IAM.

Como resolvemos aqui:
- Evitamos depender de create/list em runtime para caminho principal.
- Operacoes minimas e deterministicas: send, receive, delete-message, get-attributes.
- Preflight inicial para falhar rapido com erro acionavel.

Diretriz para eles:
- Usar URLs de fila fixas em variaveis de ambiente.
- Fazer preflight antes de iniciar teste.

### 4) Bootstrap automatico bloqueado por IAM
Problema deles:
- Tentativas de provisionar recursos no runner falham por restricao.

Como resolvemos aqui:
- Tiramos bootstrap da pipeline de teste.
- Pipeline apenas consome recursos ja provisionados.

Diretriz para eles:
- Provisionamento em IaC/esteira de plataforma separada.

### 5) Endpoint homolog sem estabilidade publica
Problema deles:
- Sem URL fixa, teste depende de fallback sensivel a permissao.

Como resolvemos aqui:
- Priorizamos endpoint estavel para o fluxo principal.
- O que nao era estavel ficou como diagnostico, nao como gate principal.

Diretriz para eles:
- Definir endpoint homolog estavel (protegido, mas acessivel pela CI).

### 6) Falhas de integracao que geram falso negativo
Como resolvemos aqui:
- Corrigimos alinhamento de prefixo S3 para nao gerar key inexistente.
- Corrigimos configuracao do provider/modelo de IA para evitar 404 de endpoint/modelo.

Diretriz para eles:
- Incluir validacao de configuracao no preflight (bucket/prefix/modelo/endpoint).

---

## Prompt pronto para o Copilot (cole no chat do outro microservico)

```text
Atue como engenheiro sênior de backend/plataforma e implemente o fechamento de E2E serverless deste microserviço em ambiente AWS com IAM restrito.

Contexto do serviço:
- Responsabilidade: receber upload de diagrama, criar processo de análise, gerenciar status.
- Entrada: HTTP REST (API Gateway).
- Assíncrono: fila para processamento.
- Persistência: tabela de processos + histórico de status.
- E2E local já existe; falta E2E serverless confiável na CI.

Restrições reais do ambiente (obrigatório considerar):
- Não depender de `eks:DescribeCluster` no caminho principal da CI.
- Não depender de `sns:CreateTopic` na execução do teste.
- Não depender de create/list de SQS em runtime para funcionar.

Implemente com este desenho:
1) Recursos estáticos por ambiente
- Usar SNS topic pré-provisionado.
- Usar SQS input/output pré-provisionadas (URLs fixas por env).
- Sem bootstrap de infra no job de teste.

2) E2E CI determinístico por contrato funcional
- Gerar `analysisId` e `correlationId` únicos.
- Enviar upload para S3 com key compatível com o prefixo configurado no serviço.
- Publicar evento de entrada na fila de input.
- Consumir fila de output e filtrar estritamente por `analysisId` + `correlationId`.
- Confirmar sucesso por evento final + consulta de status final (API e/ou DB quando permitido).

3) Fallbacks controlados
- Se DB direto não for acessível no runner, validar status por endpoint de consulta e evento final.
- Se validação via cluster exigir permissões ausentes, degradar para validação por contrato sem quebrar o job por infra externa.

4) Robustez
- Preflight antes do teste: credenciais AWS, existência de filas, permissões mínimas, bucket, endpoint API, modelo/endpoint de IA.
- Erros acionáveis: distinguir falha de negócio x falha de permissão x falha de configuração.
- Timeouts explícitos e retry com limite.

5) Observabilidade
- Logs estruturados com `analysisId`, `correlationId`, `jobId`.
- Artifacts obrigatórios: `input-event.json`, `output-message.json`, `db-check.txt` (quando houver), `summary.json`, `error-logs.json`.
- `summary.json` deve conter: `analysisId`, `correlationId`, `jobId`, `resultId`, `latencySeconds`, `status`.

6) Critérios de aceite
- Fluxo ponta a ponta executa sem criação dinâmica de SNS/SQS.
- E2E passa de forma reprodutível na CI.
- Falhas de IAM/configuração aparecem com diagnóstico claro.
- Sem regressão no E2E local já existente.

Entregue:
- Código e scripts ajustados.
- Workflow CI atualizado.
- Lista de arquivos alterados.
- Comandos de execução em homolog.
- Evidência de execução com artifacts.
```

---

## Checklist minimo de preflight para CI
- Credenciais AWS validas (`sts get-caller-identity`).
- API homolog acessivel.
- S3 bucket acessivel e com prefixo correto.
- SQS input/output existentes e com permissoes de send/receive/delete-message/get-attributes.
- SNS topic existente e com roteamento esperado para output.
- Config de IA valida (endpoint e modelo corretos).

## Resultado esperado
Com esse desenho, o time reduz dependencia de permissoes elevadas no runner e transforma o E2E serverless em teste confiavel de contrato funcional ponta a ponta, sem abrir mao de rastreabilidade.
