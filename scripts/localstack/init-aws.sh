#!/bin/sh

set -eu

awslocal sqs create-queue --queue-name analysis-process-requests >/dev/null
awslocal sqs create-queue --queue-name analysis-processing-results >/dev/null

TOPIC_ARN=$(awslocal sns create-topic --name analysis-processing-events --query TopicArn --output text)
RESULT_QUEUE_URL=$(awslocal sqs get-queue-url --queue-name analysis-processing-results --query QueueUrl --output text)
RESULT_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url "$RESULT_QUEUE_URL" --attribute-names QueueArn --query 'Attributes.QueueArn' --output text)

awslocal sns subscribe \
  --topic-arn "$TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$RESULT_QUEUE_ARN" >/dev/null

awslocal sqs set-queue-attributes \
  --queue-url "$RESULT_QUEUE_URL" \
  --attributes '{"Policy":"{\"Version\":\"2012-10-17\",\"Statement\":[{\"Sid\":\"Allow-SNS-SendMessage\",\"Effect\":\"Allow\",\"Principal\":{\"Service\":\"sns.amazonaws.com\"},\"Action\":\"sqs:SendMessage\",\"Resource\":\"'"$RESULT_QUEUE_ARN"'\",\"Condition\":{\"ArnEquals\":{\"aws:SourceArn\":\"'"$TOPIC_ARN"'\"}}}]}"}' >/dev/null