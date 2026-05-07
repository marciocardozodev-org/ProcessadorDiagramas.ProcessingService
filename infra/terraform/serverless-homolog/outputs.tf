output "lambda_function_name" {
  value = aws_lambda_function.this.function_name
}

output "lambda_function_arn" {
  value = aws_lambda_function.this.arn
}

output "input_queue_url" {
  value = aws_sqs_queue.processing_input.id
}

output "input_queue_arn" {
  value = aws_sqs_queue.processing_input.arn
}

output "output_queue_url" {
  value = aws_sqs_queue.processing_output.id
}

output "output_queue_arn" {
  value = aws_sqs_queue.processing_output.arn
}

output "processing_topic_arn" {
  value = aws_sns_topic.processing_events.arn
}

output "input_bucket_name" {
  value = aws_s3_bucket.input_diagrams.bucket
}

output "input_key_prefix" {
  value = local.input_prefix
}

output "worker_log_group" {
  value = aws_cloudwatch_log_group.lambda.name
}

output "processing_dlq_url" {
  value = aws_sqs_queue.processing_dlq.id
}
