#!/usr/bin/env bats

setup()
{
    export TESTER='../src/Tester/bin/Debug/net8.0/Tester.dll'
    export SERVER_POINT='127.0.0.1:12356'
    export TARGET='127.0.0.1:12356'
}

prepare_log()
{
    logdir="$BATS_TEST_DIRNAME/$BATS_TEST_NUMBER"
    echo "$(pwd)"
    echo "Logging to folder: $logdir"
}

run()
{
    tag=$1
    shift 1
    dotnet "$TESTER" --delta 0.05 --sample-window 20 --trace --checksum --comlog "$logdir/${tag}_com.log" --log "$logdir/${tag}_test.log" --gamelog "$logdir/${tag}_game.log" $@ &
}

@test "input receive test" {
    prepare_log
    run server rec --tickrate 20 --server --game-duration 300 --target $SERVER_POINT
    server=$!
    sleep 1
    
    run client1 rec --tickrate 20 --duration 160 --warmup 30 --target $TARGET
    client1=$!

    run client2 rec --tickrate 20 --duration 160 --warmup 30 --target $TARGET
    client2=$!

    run client3 rec --tickrate 20 --duration 160 --warmup 30 --target $TARGET
    client3=$!

    run client4 rec --tickrate 20 --duration 160 --warmup 30 --target $TARGET
    client4=$!

    run client5 rec --tickrate 20 --duration 160 --warmup 30 --target $TARGET
    client5=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
    wait -n $client5
}

@test "prediction test" {
    prepare_log

    run server pred --tickrate 20 --server --target $SERVER_POINT
    server=$!
    sleep 1
    
    run client pred --tickrate 20 --duration 200 --warmup 30 --max-lag 0.005 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}

@test "input propagation test" {
    prepare_log

    run server prop --tickrate 20 --server --duration 200 --target $SERVER_POINT
    server=$!
    sleep 1
    
    run client1 prop --tickrate 20 --count 2 --warmup 40 --target $TARGET
    client=$!

    run client2 prop --tickrate 20 --count 2 --warmup 40 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
}

@test "desync detection test" {
    prepare_log

    run server desync --tickrate 20 --server --magic 1 --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run client1 desync --tickrate 20 --magic 10 --target $TARGET
    client=$!

    run client2 desync --tickrate 20 --magic 20 --target $TARGET
    client=$!

    sleep 1

    run client3 desync --tickrate 20 --magic 30 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}

@test "kicking test" {
    prepare_log

    run server kick --tickrate 20 --server --duration 200 --target $SERVER_POINT
    server=$!
    sleep 1

    run client1 kick --tickrate 20 --target $TARGET
    client1=$!

    run client2 kick --tickrate 20 --target $TARGET
    client2=$!

    sleep 1

    run client3 kick --tickrate 20 --target $TARGET
    client3=$!

    run client4 kick --tickrate 20 --target $TARGET
    client4=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
    wait -n $client4
}

@test "input prediction test" {
    return;
    prepare_log

    run server inpr --tickrate 20 --server --duration 5 --target $SERVER_POINT
    server=$!
    sleep 1

    run client1 inpr --tickrate 20 --warmup 40 --target $TARGET
    client=$!

    run client2 inpr --tickrate 20 --warmup 40 --target $TARGET
    client=$!

    run client3 inpr --tickrate 20 --warmup 40 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client1
    wait -n $client2
    wait -n $client3
}

@test "update frequency test" {
    prepare_log

    run server freq --tickrate 20 --server --duration 20 --target $SERVER_POINT
    server=$!
    sleep 1

    run client freq --tickrate 20 --mean 0.05 --max-dev 0.001 --max-mean-error 0.001 --warmup 40 --target $TARGET
    client=$!

    wait -n $server
    wait -n $client
}
