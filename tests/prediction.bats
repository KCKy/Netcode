#!/usr/bin/env bats

load setup.bash
load functions.bash

@test "prediction test slow 1" {
    prepare_log

    run_slow_1 server pred --server --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_slow_1 client pred --duration 50 --warmup 10 --max-lag 0.01 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "prediction test slow 2" {
    prepare_log

    run_slow_2 server pred --server --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_slow_2 client pred --duration 50 --warmup 10 --max-lag 0.01 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "prediction test slow 3" {
    prepare_log

    run_slow_3 server pred --server --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_slow_3 client pred --duration 50 --warmup 10 --max-lag 0.01 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "prediction test slow 4" {
    prepare_log

    run_slow_4 server pred --server --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_slow_4 client pred --duration 50 --warmup 10 --max-lag 0.01 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}


@test "prediction test fast 1" {
    prepare_log

    run_fast_1 server pred --server --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_fast_1 client pred --duration 200 --warmup 30 --max-lag 0.01 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "prediction test fast 2" {
    prepare_log

    run_fast_2 server pred --server --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_fast_2 client pred --duration 200 --warmup 30 --max-lag 0.01 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "prediction test fast 3" {
    prepare_log

    run_fast_3 server pred --server --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_fast_3 client pred --duration 200 --warmup 30 --max-lag 0.01 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "prediction test fast 4" {
    prepare_log

    run_fast_4 server pred --server --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_fast_4 client pred --duration 200 --warmup 30 --max-lag 0.01 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}
