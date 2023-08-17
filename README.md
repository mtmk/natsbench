# NATS Bench

## Core
https://docs.nats.io/reference/reference-protocols/nats-protocol

```
S: INFO {"option_name":option_value,...}␍␊
C: CONNECT {"option_name":option_value,...}␍␊
C: PUB <subject> [reply-to] <#bytes>␍␊[payload]␍␊
C: HPUB <subject> [reply-to] <#header-bytes> <#total-bytes>␍␊[headers]␍␊␍␊[payload]␍␊
C: SUB <subject> [queue group] <sid>␍␊
C: UNSUB <sid> [max_msgs]␍␊
S: MSG <subject> <sid> [reply-to] <#bytes>␍␊[payload]␍␊
S: HMSG <subject> <sid> [reply-to] <#header-bytes> <#total-bytes>␍␊[headers]␍␊␍␊[payload]␍␊
B: PING␍␊
B: PONG␍␊
S: +OK␍␊
S: -ERR <error message>␍␊
```

## JetStream
https://docs.nats.io/reference/reference-protocols/nats_api_reference

### Account Info
```
C: SUB _INBOX.<id> <sid>
C: PUB $JS.API.INFO _INBOX.<id> 0
S: MSG _INBOX.<id> <sid> <#bytes>
   {"type":"io.nats.jetstream.api.v1.account_info_response",...}
```

### Stream Create
```
C: SUB _INBOX.<id> <sid>
C: PUB $JS.API.STREAM.CREATE.<stream> _INBOX.<id> <#bytes>
   {"name":"s2","subjects":["foo.*"],"retention":"limits",...}
S: MSG _INBOX.<id> <sid> <#bytes>
   {"type":"io.nats.jetstream.api.v1.stream_create_response",...}
```

### Pull Next
```
C: SUB _INBOX.<id> <sid>␍␊
C: PUB $JS.API.CONSUMER.MSG.NEXT.<stream>.<consumer> _INBOX.<id> 0␍␊
S: MSG <stream-subject> <sid> $JS.ACK.<stream>.<consumer>.3.1.3.1692271055168144700.0 <#bytes>␍␊[payload]␍␊
```
