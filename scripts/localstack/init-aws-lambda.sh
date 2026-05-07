#!/bin/sh

set -eu

# Bucket S3 para diagramas de entrada
awslocal s3 mb s3://lambda-input-diagrams >/dev/null

# Fila de entrada da Lambda (não usada pelo poller, mas útil para testes manuais via SQS)
awslocal sqs create-queue --queue-name lambda-input >/dev/null

# Fila de resultados — onde a Lambda publica via SNS subscription
awslocal sqs create-queue --queue-name lambda-results >/dev/null

# Tópico SNS de eventos de processamento
TOPIC_ARN=$(awslocal sns create-topic --name lambda-processing-events --query TopicArn --output text)

# Subscrição SNS→SQS para resultados
RESULTS_QUEUE_URL=$(awslocal sqs get-queue-url --queue-name lambda-results --query QueueUrl --output text)
RESULTS_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url "$RESULTS_QUEUE_URL" \
  --attribute-names QueueArn \
  --query 'Attributes.QueueArn' --output text)

awslocal sns subscribe \
  --topic-arn "$TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$RESULTS_QUEUE_ARN" >/dev/null

awslocal sqs set-queue-attributes \
  --queue-url "$RESULTS_QUEUE_URL" \
  --attributes "{\"Policy\":\"{\\\"Version\\\":\\\"2012-10-17\\\",\\\"Statement\\\":[{\\\"Sid\\\":\\\"Allow-SNS\\\",\\\"Effect\\\":\\\"Allow\\\",\\\"Principal\\\":{\\\"Service\\\":\\\"sns.amazonaws.com\\\"},\\\"Action\\\":\\\"sqs:SendMessage\\\",\\\"Resource\\\":\\\"$RESULTS_QUEUE_ARN\\\",\\\"Condition\\\":{\\\"ArnEquals\\\":{\\\"aws:SourceArn\\\":\\\"$TOPIC_ARN\\\"}}}]}\"}" >/dev/null

echo "[localstack] Lambda stack inicializada: S3=lambda-input-diagrams, SQS=lambda-input/lambda-results, SNS=$TOPIC_ARN"
