#!/usr/bin/env bats

load setup.bash
load functions.bash

@test "input prediction test slow 1" {
    prepare_log

    run_slow_1 server inpr --server --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run_slow_1 client1 inpr --warmup 10 --target $TARGET
    client1=$!

    run_slow_1 client2 inpr --warmup 10 --target $TARGET
    client2=$!

    run_slow_1 client3 inpr --warmup 10 --target $TARGET
    client2=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}

@test "input prediction test slow 2" {
    prepare_log

    run_slow_2 server inpr --server --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run_slow_2 client1 inpr --warmup 10 --target $TARGET
    client1=$!

    run_slow_2 client2 inpr --warmup 10 --target $TARGET
    client2=$!

    run_slow_2 client3 inpr --warmup 10 --target $TARGET
    client3=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}

@test "input prediction test slow 3" {
    prepare_log

    run_slow_3 server inpr --server --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run_slow_3 client1 inpr --warmup 10 --target $TARGET
    client1=$!

    run_slow_3 client2 inpr --warmup 10 --target $TARGET
    client2=$!

    run_slow_3 client3 inpr --warmup 10 --target $TARGET
    client3=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}

@test "input prediction test slow 4" {
    prepare_log

    run_slow_4 server inpr --server --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run_slow_4 client1 inpr --warmup 10 --target $TARGET
    client1=$!

    run_slow_4 client2 inpr --warmup 10 --target $TARGET
    client2=$!

    run_slow_4 client3 inpr --warmup 10 --target $TARGET
    client3=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}

@test "input prediction test fast 1" {
    prepare_log

    run_fast_1 server inpr --server --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_1 client1 inpr --warmup 40 --target $TARGET
    client1=$!

    run_fast_1 client2 inpr --warmup 40 --target $TARGET
    client2=$!

    run_fast_1 client3 inpr --warmup 40 --target $TARGET
    client3=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}


@test "input prediction test fast 2" {
    prepare_log

    run_fast_2 server inpr --server --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_2 client1 inpr --warmup 40 --target $TARGET
    client1=$!

    run_fast_2 client2 inpr --warmup 40 --target $TARGET
    client2=$!

    run_fast_2 client3 inpr --warmup 40 --target $TARGET
    client3=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}

@test "input prediction test fast 3" {
    prepare_log

    run_fast_3 server inpr --server --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_3 client1 inpr --warmup 40 --target $TARGET
    client1=$!

    run_fast_3 client2 inpr --warmup 40 --target $TARGET
    client2=$!

    run_fast_3 client3 inpr --warmup 40 --target $TARGET
    client3=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}

@test "input prediction test fast 4" {
    prepare_log

    run_fast_4 server inpr --server --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run_fast_4 client1 inpr --warmup 40 --target $TARGET
    client1=$!

    run_fast_4 client2 inpr --warmup 40 --target $TARGET
    client2=$!

    run_fast_4 client3 inpr --warmup 40 --target $TARGET
    client3=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}
