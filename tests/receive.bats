#!/usr/bin/env bats

load setup.bash
load functions.bash

@test "input receive test slow 1" {
    prepare_log
    run_slow_1 server rec --server --game-duration 120 --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_slow_1 client1 rec --duration 80 --warmup 10 --target $TARGET
    client1=$!

    run_slow_1 client2 rec --duration 80 --warmup 10 --target $TARGET
    client2=$!

    run_slow_1 client3 rec --duration 80 --warmup 10 --target $TARGET
    client3=$!

    run_slow_1 client4 rec --duration 80 --warmup 10 --target $TARGET
    client4=$!

    run_slow_1 client5 rec --duration 80 --warmup 10 --target $TARGET
    client5=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
    wait -n $client5
}

@test "input receive test slow 2" {
    prepare_log
    run_slow_2 server rec --server --game-duration 120 --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_slow_2 client1 rec --duration 80 --warmup 10 --target $TARGET
    client1=$!

    run_slow_2 client2 rec --duration 80 --warmup 10 --target $TARGET
    client2=$!

    run_slow_2 client3 rec --duration 80 --warmup 10 --target $TARGET
    client3=$!

    run_slow_2 client4 rec --duration 80 --warmup 10 --target $TARGET
    client4=$!

    run_slow_2 client5 rec --duration 80 --warmup 10 --target $TARGET
    client5=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
    wait -n $client5
}

@test "input receive test slow 3" {
    prepare_log
    run_slow_3 server rec --server --game-duration 120 --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_slow_3 client1 rec --duration 80 --warmup 10 --target $TARGET
    client1=$!

    run_slow_3 client2 rec --duration 80 --warmup 10 --target $TARGET
    client2=$!

    run_slow_3 client3 rec --duration 80 --warmup 10 --target $TARGET
    client3=$!

    run_slow_3 client4 rec --duration 80 --warmup 10 --target $TARGET
    client4=$!

    run_slow_3 client5 rec --duration 80 --warmup 10 --target $TARGET
    client5=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
    wait -n $client5
}

@test "input receive test slow 4" {
    prepare_log
    run_slow_4 server rec --server --game-duration 120 --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_slow_4 client1 rec --duration 80 --warmup 10 --target $TARGET
    client1=$!

    run_slow_4 client2 rec --duration 80 --warmup 10 --target $TARGET
    client2=$!

    run_slow_4 client3 rec --duration 80 --warmup 10 --target $TARGET
    client3=$!

    run_slow_4 client4 rec --duration 80 --warmup 10 --target $TARGET
    client4=$!

    run_slow_4 client5 rec --duration 80 --warmup 10 --target $TARGET
    client5=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
    wait -n $client5
}

@test "input receive test fast 1" {
    prepare_log
    run_fast_1 server rec --server --game-duration 300 --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_fast_1 client1 rec --duration 240 --warmup 30 --target $TARGET
    client1=$!

    run_fast_1 client2 rec --duration 240 --warmup 30 --target $TARGET
    client2=$!

    run_fast_1 client3 rec --duration 240 --warmup 30 --target $TARGET
    client3=$!

    run_fast_1 client4 rec --duration 240 --warmup 30 --target $TARGET
    client4=$!

    run_fast_1 client5 rec --duration 240 --warmup 30 --target $TARGET
    client5=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
    wait -n $client5
}

@test "input receive test fast 2" {
    prepare_log
    run_fast_2 server rec --server --game-duration 300 --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_fast_2 client1 rec --duration 240 --warmup 30 --target $TARGET
    client1=$!

    run_fast_2 client2 rec --duration 240 --warmup 30 --target $TARGET
    client2=$!

    run_fast_2 client3 rec --duration 240 --warmup 30 --target $TARGET
    client3=$!

    run_fast_2 client4 rec --duration 240 --warmup 30 --target $TARGET
    client4=$!

    run_fast_2 client5 rec --duration 240 --warmup 30 --target $TARGET
    client5=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
    wait -n $client5
}

@test "input receive test fast 3" {
    prepare_log
    run_fast_3 server rec --server --game-duration 300 --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_fast_3 client1 rec --duration 240 --warmup 30 --target $TARGET
    client1=$!

    run_fast_3 client2 rec --duration 240 --warmup 30 --target $TARGET
    client2=$!

    run_fast_3 client3 rec --duration 240 --warmup 30 --target $TARGET
    client3=$!

    run_fast_3 client4 rec --duration 240 --warmup 30 --target $TARGET
    client4=$!

    run_fast_3 client5 rec --duration 240 --warmup 30 --target $TARGET
    client5=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
    wait -n $client5
}

@test "input receive test fast 4" {
    prepare_log
    run_fast_4 server rec --server --game-duration 300 --target $SERVER_POINT
    server=$!
    sleep 1
    
    run_fast_4 client1 rec --duration 240 --warmup 30 --target $TARGET
    client1=$!

    run_fast_4 client2 rec --duration 240 --warmup 30 --target $TARGET
    client2=$!

    run_fast_4 client3 rec --duration 240 --warmup 30 --target $TARGET
    client3=$!

    run_fast_4 client4 rec --duration 240 --warmup 30 --target $TARGET
    client4=$!

    run_fast_4 client5 rec --duration 240 --warmup 30 --target $TARGET
    client5=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
    wait -n $client5
}
