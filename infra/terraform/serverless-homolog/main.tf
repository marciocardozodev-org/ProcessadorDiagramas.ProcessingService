locals {
  app_name        = "${var.name_prefix}-${var.environment}"
  log_group_name  = "/aws/lambda/${local.app_name}"
  input_prefix    = "input-diagrams"
  output_sub_name = "${local.app_name}-output-subscription"
}

resource "aws_s3_bucket" "input_diagrams" {
  bucket_prefix = "${local.app_name}-input-"
}

resource "aws_s3_bucket_public_access_block" "input_diagrams" {
  bucket                  = aws_s3_bucket.input_diagrams.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_versioning" "input_diagrams" {
  bucket = aws_s3_bucket.input_diagrams.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_sqs_queue" "processing_dlq" {
  name                      = "${local.app_name}-processing-dlq"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "processing_input" {
  name                       = "${local.app_name}-processing-input"
  # visibility timeout deve ser >= 6x o timeout da Lambda (boas práticas AWS)
  visibility_timeout_seconds = var.lambda_timeout * 6
  receive_wait_time_seconds  = 20

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.processing_dlq.arn
    maxReceiveCount     = 5
  })
}

resource "aws_sqs_queue" "processing_output" {
  name                       = "${local.app_name}-processing-output"
  visibility_timeout_seconds = 60
  receive_wait_time_seconds  = 10
}

resource "aws_sns_topic" "processing_events" {
  name = "${local.app_name}-processing-events"
}

resource "aws_sns_topic_subscription" "output_queue" {
  topic_arn = aws_sns_topic.processing_events.arn
  protocol  = "sqs"
  endpoint  = aws_sqs_queue.processing_output.arn
}

resource "aws_sqs_queue_policy" "processing_output_policy" {
  queue_url = aws_sqs_queue.processing_output.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "AllowSnsPublish"
        Effect    = "Allow"
        Principal = "*"
        Action    = "sqs:SendMessage"
        Resource  = aws_sqs_queue.processing_output.arn
        Condition = {
          ArnEquals = {
            "aws:SourceArn" = aws_sns_topic.processing_events.arn
          }
        }
      }
    ]
  })
}

resource "aws_cloudwatch_log_group" "lambda" {
  name              = local.log_group_name
  retention_in_days = 14
}

resource "aws_cloudwatch_log_metric_filter" "error_count" {
  name           = "${local.app_name}-errors"
  log_group_name = aws_cloudwatch_log_group.lambda.name
  pattern        = "{ $.LogLevel = \"Error\" }"

  metric_transformation {
    name      = "${local.app_name}-error-count"
    namespace = "ProcessadorDiagramas/ProcessingService"
    value     = "1"
  }
}

resource "aws_cloudwatch_metric_alarm" "error_rate_high" {
  alarm_name          = "${local.app_name}-high-error-rate"
  alarm_description   = "High error count in processing worker logs"
  namespace           = "ProcessadorDiagramas/ProcessingService"
  metric_name         = aws_cloudwatch_log_metric_filter.error_count.metric_transformation[0].name
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 5
  comparison_operator = "GreaterThanOrEqualToThreshold"
  alarm_actions       = var.alarm_actions
}

resource "aws_cloudwatch_metric_alarm" "dlq_backlog" {
  alarm_name          = "${local.app_name}-dlq-backlog"
  alarm_description   = "DLQ backlog indicates processing failures"
  namespace           = "AWS/SQS"
  metric_name         = "ApproximateNumberOfMessagesVisible"
  statistic           = "Maximum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 1
  comparison_operator = "GreaterThanOrEqualToThreshold"
  alarm_actions       = var.alarm_actions

  dimensions = {
    QueueName = aws_sqs_queue.processing_dlq.name
  }
}

resource "aws_cloudwatch_metric_alarm" "latency_abnormal" {
  alarm_name          = "${local.app_name}-queue-latency-anomalous"
  alarm_description   = "Input queue oldest message age indicates abnormal latency"
  namespace           = "AWS/SQS"
  metric_name         = "ApproximateAgeOfOldestMessage"
  statistic           = "Maximum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 120
  comparison_operator = "GreaterThanOrEqualToThreshold"
  alarm_actions       = var.alarm_actions

  dimensions = {
    QueueName = aws_sqs_queue.processing_input.name
  }
}

resource "aws_iam_role" "lambda_exec" {
  name = "${local.app_name}-lambda-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "lambda_basic_execution" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_iam_role_policy_attachment" "lambda_vpc_execution" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

resource "aws_iam_role_policy" "lambda_app_policy" {
  name = "${local.app_name}-lambda-app-policy"
  role = aws_iam_role.lambda_exec.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "SqsConsume"
        Effect = "Allow"
        Action = [
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes",
          "sqs:ChangeMessageVisibility"
        ]
        Resource = [
          aws_sqs_queue.processing_input.arn,
          aws_sqs_queue.processing_dlq.arn
        ]
      },
      {
        Sid    = "SnsPublish"
        Effect = "Allow"
        Action = ["sns:Publish"]
        Resource = aws_sns_topic.processing_events.arn
      },
      {
        Sid    = "S3Read"
        Effect = "Allow"
        Action = ["s3:GetObject"]
        Resource = ["${aws_s3_bucket.input_diagrams.arn}/*"]
      },
      {
        Sid    = "SsmRead"
        Effect = "Allow"
        Action = ["ssm:GetParameter"]
        Resource = [var.db_connection_ssm_parameter_arn]
      },
      {
        Sid    = "SecretsRead"
        Effect = "Allow"
        Action = ["secretsmanager:GetSecretValue"]
        Resource = [var.ai_api_key_secret_arn]
      },
      {
        Sid    = "KmsDecrypt"
        Effect = "Allow"
        Action = ["kms:Decrypt"]
        Resource = ["*"]
      }
    ]
  })
}

resource "aws_lambda_function" "this" {
  function_name = local.app_name
  role          = aws_iam_role.lambda_exec.arn
  package_type  = "Image"
  image_uri     = var.image_uri
  timeout       = var.lambda_timeout
  memory_size   = var.lambda_memory_size

  reserved_concurrent_executions = var.lambda_reserved_concurrency

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT               = "Homolog"
      EnableAwsServices                    = "true"
      Aws__Region                          = var.aws_region
      Aws__TopicArn                        = aws_sns_topic.processing_events.arn
      Aws__DbConnectionParameterArn        = var.db_connection_ssm_parameter_arn
      Aws__AiApiKeySecretArn               = var.ai_api_key_secret_arn
      Aws__OperationRetryMaxAttempts       = "3"
      Aws__RetryBaseDelayMilliseconds      = "250"
      Aws__RetryMaxDelaySeconds            = "15"
      DiagramSourceStorage__Provider       = "S3"
      DiagramSourceStorage__BucketName     = aws_s3_bucket.input_diagrams.bucket
      DiagramSourceStorage__KeyPrefix      = local.input_prefix
      AiProvider__Enabled                  = "true"
      AiProvider__Provider                 = "OpenAICompatible"
      AiProvider__BaseUrl                  = var.ai_provider_base_url
      AiProvider__Model                    = var.ai_provider_model
      AiProvider__Temperature              = var.ai_provider_temperature
      AiProvider__MaxTokens                = var.ai_provider_max_tokens
      AiProvider__MaxInputCharacters       = var.ai_provider_max_input_characters
      AiProvider__TimeoutSeconds           = var.ai_provider_timeout_seconds
    }
  }

  vpc_config {
    subnet_ids         = var.subnet_ids
    security_group_ids = var.security_group_ids
  }

  logging_config {
    log_group  = aws_cloudwatch_log_group.lambda.name
    log_format = "Text"
  }

  depends_on = [
    aws_iam_role_policy_attachment.lambda_basic_execution,
    aws_iam_role_policy_attachment.lambda_vpc_execution,
    aws_cloudwatch_log_group.lambda,
  ]
}

resource "aws_lambda_event_source_mapping" "sqs_trigger" {
  event_source_arn                   = aws_sqs_queue.processing_input.arn
  function_name                      = aws_lambda_function.this.arn
  batch_size                         = 10
  maximum_batching_window_in_seconds = 5
  function_response_types            = ["ReportBatchItemFailures"]
  enabled                            = true
}
